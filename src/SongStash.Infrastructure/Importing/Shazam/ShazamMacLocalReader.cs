using System.Globalization;
using Microsoft.Data.Sqlite;
using SongStash.Application.Importing;
using SongStash.Domain.ExternalReferences;
using SongStash.Domain.Finds;

namespace SongStash.Infrastructure.Importing.Shazam;

/// <summary>Reads the local macOS Shazam history database without modifying it.</summary>
public sealed class ShazamMacLocalReader : IImportSourceReader
{
    private const string TableName = "ZSHTRACKMO";
    private const double FoundationToUnixSeconds = 978307200d;
    private const double MinimumFoundationSeconds = -3187296000d; // 1900-01-01
    private const double MaximumFoundationSeconds = 3124137600d; // 2100-01-01

    private static readonly string[] RequiredColumns = ["ZTITLE", "ZSUBTITLE", "ZDATE"];
    private static readonly string[] OptionalColumns =
    [
        "ZALBUMNAME",
        "ZARTWORKURL",
        "ZRECOGNITIONID",
        "ZAPPLEMUSICID",
        "ZISRC",
        "ZSHAZAMKEY",
        "ZSHAZAMURL"
    ];

    private readonly string _databasePath;

    public ShazamMacLocalReader() : this(GetDefaultDatabasePath())
    {
    }

    /// <summary>Accepts a path override for isolated tests and diagnostics.</summary>
    public ShazamMacLocalReader(string databasePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(databasePath);
        _databasePath = databasePath;
    }

    public async Task<ImportReadResult> ReadAsync(CancellationToken cancellationToken = default)
    {
        if (!OperatingSystem.IsMacOS())
        {
            return ImportReadResult.Unavailable(Diagnostic(
                ImportDiagnosticSeverity.Error,
                "source-platform-unsupported",
                "The local Shazam database reader is available only on macOS."));
        }

        if (!File.Exists(_databasePath))
        {
            return ImportReadResult.Unavailable(Diagnostic(
                ImportDiagnosticSeverity.Error,
                "source-unavailable",
                "The local Shazam database was not found."));
        }

        try
        {
            await using var connection = new SqliteConnection(new SqliteConnectionStringBuilder
            {
                DataSource = _databasePath,
                Mode = SqliteOpenMode.ReadOnly,
                Cache = SqliteCacheMode.Private
            }.ToString());

            await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
            await EnableQueryOnlyAsync(connection, cancellationToken).ConfigureAwait(false);

            var schema = await ReadSchemaAsync(connection, cancellationToken).ConfigureAwait(false);
            if (!schema.TableExists)
            {
                return UnsupportedSchema("The Shazam history table is missing.");
            }

            var missingColumns = RequiredColumns.Where(column => !schema.Columns.Contains(column)).ToArray();
            if (missingColumns.Length > 0)
            {
                return UnsupportedSchema("The Shazam history table is missing required columns.");
            }

            var candidates = await ReadCandidatesAsync(connection, schema.Columns, cancellationToken)
                .ConfigureAwait(false);
            return ImportReadResult.Success(candidates);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (SqliteException exception)
        {
            var diagnostic = Diagnostic(
                ImportDiagnosticSeverity.Error,
                exception.SqliteErrorCode == 14 ? "source-unavailable" : "source-read-failed",
                "The local Shazam database could not be read.");
            return ImportReadResult.Unavailable(diagnostic);
        }
        catch (UnauthorizedAccessException)
        {
            return ImportReadResult.Unavailable(Diagnostic(
                ImportDiagnosticSeverity.Error,
                "source-unavailable",
                "The local Shazam database is not accessible."));
        }
        catch (IOException)
        {
            return ImportReadResult.Unavailable(Diagnostic(
                ImportDiagnosticSeverity.Error,
                "source-unavailable",
                "The local Shazam database could not be accessed."));
        }
    }

    private static string GetDefaultDatabasePath() => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
        "Library",
        "Application Support",
        "com.apple.shazamd",
        "ShazamLibrary.sqlite");

    private static async Task EnableQueryOnlyAsync(
        SqliteConnection connection,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = "PRAGMA query_only=ON;";
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    private static async Task<SchemaInfo> ReadSchemaAsync(
        SqliteConnection connection,
        CancellationToken cancellationToken)
    {
        await using var tableCommand = connection.CreateCommand();
        tableCommand.CommandText = "SELECT 1 FROM sqlite_master WHERE type='table' AND name=$table LIMIT 1;";
        tableCommand.Parameters.AddWithValue("$table", TableName);
        var tableExists = await tableCommand.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false) is not null;
        if (!tableExists)
        {
            return new SchemaInfo(false, new HashSet<string>(StringComparer.OrdinalIgnoreCase));
        }

        await using var columnsCommand = connection.CreateCommand();
        columnsCommand.CommandText = "PRAGMA table_info(ZSHTRACKMO);";
        await using var reader = await columnsCommand.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        var columns = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            columns.Add(reader.GetString(1));
        }

        return new SchemaInfo(true, columns);
    }

    private static async Task<IReadOnlyList<ImportCandidate>> ReadCandidatesAsync(
        SqliteConnection connection,
        IReadOnlySet<string> availableColumns,
        CancellationToken cancellationToken)
    {
        var selectedColumns = RequiredColumns.Concat(OptionalColumns).ToArray();
        var projection = string.Join(", ", selectedColumns.Select(column =>
            availableColumns.Contains(column) ? $"\"{column}\"" : $"NULL AS \"{column}\""));

        await using var command = connection.CreateCommand();
        command.CommandText = $"SELECT {projection} FROM \"{TableName}\";";
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        var ordinals = selectedColumns
            .Select((column, index) => (column, index))
            .ToDictionary(pair => pair.column, pair => pair.index, StringComparer.OrdinalIgnoreCase);

        var candidates = new List<ImportCandidate>();
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            cancellationToken.ThrowIfCancellationRequested();
            candidates.Add(MapCandidate(reader, ordinals));
        }

        return candidates;
    }

    private static ImportCandidate MapCandidate(SqliteDataReader reader, IReadOnlyDictionary<string, int> ordinals)
    {
        var diagnostics = new List<ImportDiagnostic>();
        var title = ReadText(reader, ordinals["ZTITLE"]) ?? string.Empty;
        var artist = ReadText(reader, ordinals["ZSUBTITLE"]) ?? string.Empty;

        if (title.Length == 0 || artist.Length == 0)
        {
            diagnostics.Add(Diagnostic(
                ImportDiagnosticSeverity.Warning,
                "incomplete-required-metadata",
                "The source row is missing a title or artist."));
        }

        var artworkUrl = ParseUri(ReadText(reader, ordinals["ZARTWORKURL"]), "invalid-artwork-url", diagnostics);
        var foundAt = ParseFoundationDate(reader, ordinals["ZDATE"], diagnostics);
        var references = ReadExternalReferences(reader, ordinals, diagnostics);

        return new ImportCandidate(title, artist, FindSource.ShazamApple)
        {
            Album = ReadText(reader, ordinals["ZALBUMNAME"]),
            ArtworkUrl = artworkUrl,
            FoundAt = foundAt,
            SourceItemId = ReadText(reader, ordinals["ZRECOGNITIONID"]),
            ExternalReferences = references,
            Diagnostics = diagnostics
        };
    }

    private static List<ExternalReferenceCandidate> ReadExternalReferences(
        SqliteDataReader reader,
        IReadOnlyDictionary<string, int> ordinals,
        ICollection<ImportDiagnostic> diagnostics)
    {
        var references = new List<ExternalReferenceCandidate>();
        AddTextReference(reader, ordinals["ZAPPLEMUSICID"], ExternalReferenceProvider.AppleMusic, references, diagnostics);
        AddTextReference(reader, ordinals["ZISRC"], ExternalReferenceProvider.Isrc, references, diagnostics);

        var shazamKey = ReadText(reader, ordinals["ZSHAZAMKEY"]);
        var shazamUrlText = ReadText(reader, ordinals["ZSHAZAMURL"]);
        var shazamUrl = ParseUri(shazamUrlText, "malformed-external-reference", diagnostics);
        if (shazamKey is not null)
        {
            references.Add(new ExternalReferenceCandidate(ExternalReferenceProvider.Shazam, shazamKey, shazamUrl));
        }
        else if (shazamUrl is not null)
        {
            references.Add(new ExternalReferenceCandidate(
                ExternalReferenceProvider.Shazam,
                shazamUrl.AbsoluteUri,
                shazamUrl));
        }

        return references;
    }

    private static void AddTextReference(
        SqliteDataReader reader,
        int ordinal,
        ExternalReferenceProvider provider,
        ICollection<ExternalReferenceCandidate> references,
        ICollection<ImportDiagnostic> diagnostics)
    {
        var value = ReadText(reader, ordinal);
        if (value is not null)
        {
            references.Add(new ExternalReferenceCandidate(provider, value));
        }
        else if (!reader.IsDBNull(ordinal))
        {
            diagnostics.Add(Diagnostic(
                ImportDiagnosticSeverity.Warning,
                "malformed-external-reference",
                "An external reference was present but empty."));
        }
    }

    private static Uri? ParseUri(
        string? value,
        string diagnosticCode,
        ICollection<ImportDiagnostic> diagnostics)
    {
        if (value is null)
        {
            return null;
        }

        if (Uri.TryCreate(value, UriKind.Absolute, out var uri))
        {
            return uri;
        }

        diagnostics.Add(Diagnostic(
            ImportDiagnosticSeverity.Warning,
            diagnosticCode,
            "A source URL was present but invalid."));
        return null;
    }

    private static DateTimeOffset? ParseFoundationDate(
        SqliteDataReader reader,
        int ordinal,
        ICollection<ImportDiagnostic> diagnostics)
    {
        if (reader.IsDBNull(ordinal))
        {
            diagnostics.Add(InvalidTimestampDiagnostic());
            return null;
        }

        try
        {
            var raw = reader.GetValue(ordinal);
            double foundationSeconds;
            if (raw is string text)
            {
                if (!double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out foundationSeconds))
                {
                    diagnostics.Add(InvalidTimestampDiagnostic());
                    return null;
                }
            }
            else if (raw is IConvertible convertible)
            {
                foundationSeconds = convertible.ToDouble(CultureInfo.InvariantCulture);
            }
            else
            {
                diagnostics.Add(InvalidTimestampDiagnostic());
                return null;
            }

            if (!double.IsFinite(foundationSeconds) ||
                foundationSeconds < MinimumFoundationSeconds ||
                foundationSeconds > MaximumFoundationSeconds)
            {
                diagnostics.Add(InvalidTimestampDiagnostic());
                return null;
            }

            return DateTimeOffset.UnixEpoch.AddSeconds(foundationSeconds + FoundationToUnixSeconds);
        }
        catch (Exception exception) when (exception is FormatException or InvalidCastException or OverflowException or ArgumentOutOfRangeException)
        {
            diagnostics.Add(InvalidTimestampDiagnostic());
            return null;
        }
    }

    private static ImportDiagnostic InvalidTimestampDiagnostic() => Diagnostic(
        ImportDiagnosticSeverity.Warning,
        "invalid-timestamp",
        "The source timestamp was missing or could not be converted safely.");

    private static string? ReadText(SqliteDataReader reader, int ordinal)
    {
        if (reader.IsDBNull(ordinal))
        {
            return null;
        }

        var value = Convert.ToString(reader.GetValue(ordinal), CultureInfo.InvariantCulture)?.Trim();
        return string.IsNullOrEmpty(value) ? null : value;
    }

    private static ImportReadResult UnsupportedSchema(string message) => ImportReadResult.UnsupportedSource(
        Diagnostic(ImportDiagnosticSeverity.Error, "unsupported-schema", message));

    private static ImportDiagnostic Diagnostic(
        ImportDiagnosticSeverity severity,
        string code,
        string message) => new(severity, code, message);

    private sealed record SchemaInfo(bool TableExists, HashSet<string> Columns);
}
