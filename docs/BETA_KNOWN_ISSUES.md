# SongStash Beta Known Issues

This list contains confirmed limitations that remain after the Stage 16.1 hardening pass.

- Packaged Shazam import has been validated on the current test setup. Compatibility across different Macs and future macOS or Shazam database versions is not guaranteed. This beta is not sandboxed or notarized.
- Launch-at-startup is currently a persisted preference only; operating-system registration is not implemented.
- Import completion and issue notification preferences are persisted, but operating-system notification delivery is not implemented.
- Artwork URLs are stored and surfaced in read models; there is no local artwork cache/offline image store.
- Tags can be stored, but the Song-to-Tag relationship is not implemented.
- Stability of Shazam recognition IDs across future Shazam database versions has not been established. They are useful event identifiers for current experiments, not a long-term compatibility guarantee.

