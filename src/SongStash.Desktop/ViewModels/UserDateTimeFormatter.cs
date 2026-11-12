using System.Globalization;

namespace SongStash.Desktop.ViewModels;

public static class UserDateTimeFormatter
{
    public static string Format(DateTimeOffset value) =>
        value.ToLocalTime().ToString("dd/MM/yyyy HH:mm", CultureInfo.InvariantCulture);
}
