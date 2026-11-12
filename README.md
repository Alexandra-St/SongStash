# SongStash

SongStash is a local-first macOS archive for music you've discovered. Shazam identifies a track; SongStash keeps its discovery history, including repeated finds.

**Current release:** 0.1.0-beta.1  
**Platform:** macOS, Apple Silicon

<!-- Add one screenshot of the running app here when a verified runtime capture is available. -->

## Why SongStash?

Shazam is great at identifying tracks, but its history is not designed as a long-term personal archive. A `Song` represents a track; each `FindEvent` represents one discovery. If a song is recognized three times, the library keeps three events instead of collapsing them into one.

## Features

- Import local Shazam history on macOS, or import CSV and JSON files with a preview, progress, and results flow.
- Keep repeated recognitions as separate find events.
- Browse a library with song details and find history; search, sort, or add a song manually.
- Open songs with a preferred music service.
- Export CSV or JSON and create local backups.
- Use the interface in English or Russian, with four themes and Follow System.

## Architecture

```text
Song 1 ─── many FindEvents
```

`Song` stores track details. `FindEvent` stores an individual recognition or discovery, so repeat finds remain distinct.

- **Domain** — songs, find events, and external references.
- **Application** — import, matching, library, settings, and backup contracts.
- **Infrastructure** — SQLite persistence, Shazam and file readers, export, and backup.
- **Desktop** — Avalonia UI, view models, and macOS app entry point.

## Technology

C# · .NET 10 · Avalonia 12 · EF Core 10 · SQLite · CommunityToolkit.Mvvm · xUnit

## macOS and Shazam

The current beta targets Apple Silicon Macs. SongStash reads the local Shazam history database in read-only mode and does not modify it. SongStash needs no account or backend; library data and settings are stored locally.

## Build and test

With the .NET 10 SDK installed:

```sh
dotnet build
dotnet test
dotnet run --project src/SongStash.Desktop
```

The solution includes unit and integration tests for the domain, import pipeline, persistence, settings, backup, and desktop view models.

## Beta status

SongStash is currently in closed beta for Apple Silicon Macs. The current focus is beta feedback and compatibility testing across different Macs.

## Licensing

SongStash is source available.

The source code is licensed under the PolyForm Noncommercial License 1.0.0.

Personal and other permitted noncommercial use and modification are allowed under the license.

Commercial use requires separate permission from Alexandra Strona.

Visual assets are subject to separate terms.

- [LICENSE](LICENSE)
- [LICENSING.md](LICENSING.md)
- [ASSETS_LICENSE.md](ASSETS_LICENSE.md)
- [THIRD_PARTY_NOTICES.md](THIRD_PARTY_NOTICES.md)
