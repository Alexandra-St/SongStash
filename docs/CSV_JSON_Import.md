# CSV and JSON metadata import (beta)

SongStash accepts UTF-8 CSV (`.csv`) and JSON (`.json`) metadata files. Files go through the normal preview, matching, event deduplication, and import flow. Rows are not grouped by the reader.

## CSV

The first non-empty record is a header. `Title` and `Artist` columns are required. Optional columns are `Album`, `ArtworkUrl`, `FoundAt`, `Source`, `SourceItemId`, `ShazamId`, `ShazamUrl`, `AppleMusicId`, and `Isrc`. `Song` and `Track` are aliases for `Title`; `Performer` is an alias for `Artist`; `DateFound` and `FoundDate` are aliases for `FoundAt`. Header names are case-insensitive. Commas, quotes, and newlines inside quoted cells follow standard CSV quoting rules.

## JSON

JSON must be either an array of flat records or an object with a `songs` array of flat records. Property names are case-insensitive. Each record uses the same fields as CSV, in camelCase or PascalCase (for example `title`, `artist`, `foundAt`, `sourceItemId`, and `shazamId`). Other properties are ignored.

## Values

`Source` is a `FindSource` name (`ShazamApple`, `ShazamAndroid`, `Manual`, or `ImportedUnknown`). Missing values use `ImportedUnknown`; unrecognized values also use it and add a warning. CSV/JSON describe the transport, not the origin. A Shazam track ID becomes an external reference and never becomes `SourceItemId`.

`FoundAt` must be an ISO-8601 timestamp with an explicit timezone offset (including `Z`). A date-only value or a timestamp without an offset is left unknown with a warning. External IDs and Shazam URLs are mapped to Shazam, Apple Music, and ISRC references.

## Export compatibility

The current SongStash CSV export can be read as generic metadata. Each exported event row becomes a candidate; song-level columns such as genre, notes, and favorite are ignored. Multiple exported IDs in one cell are accepted as a JSON string array.

The current SongStash JSON export has a `songs` array, so its top-level song metadata is read. Its `findEvents` and `externalReferences` are nested arrays and are intentionally ignored by the generic reader; the reader creates at most one generic candidate per song and does not restore those nested records. Use a future backup/restore feature when preserving a complete library is required. This importer is not a backup restore tool.
