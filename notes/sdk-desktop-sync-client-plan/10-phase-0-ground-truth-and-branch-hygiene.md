## Phase 0 - Ground Truth And Branch Hygiene

- [x] Start from `develop` and create a dedicated feature branch.
  Verification 2026-06-03: branch `feature/desktop-sync-client`; `git status --short --branch` reported `## feature/desktop-sync-client...origin/feature/desktop-sync-client [ahead 7]`.
- [ ] Review the current `Cotton.Sync`, `Cotton.Sdk`, `Cotton.Sync.Cli`, and `Cotton.Sync.Desktop` code.
  Verification: record the existing classes that will be reused and the classes that must be replaced.
- [ ] Review current server file/node/chunk/auth/SignalR endpoints.
  Verification: record concrete endpoint names and DTOs. Do not guess model fields.
- [ ] Review existing tests before changing architecture.
  Verification: record current passing/failing test baseline.
- [ ] Confirm local dev server startup path and test credentials strategy.
  Verification: record the command and whether public-instance auto-create is used.
