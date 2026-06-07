## Current Work Order

Use this section as the active checklist for the current desktop-client pass. Do not jump to a lower item while a higher item has an unresolved correctness or usability blocker.

- [ ] Stabilize sync controls during active work.
  Required behavior: global pause/resume is available while synchronization is running, disabling one pair does not resume other pairs after a global pause, and paused state survives application restart.
  Current status: code-side command/state audit is done; keep open for Windows two-pair manual verification.
  Verification: focused command/state tests plus Windows manual check with two sync pairs.
- [ ] Stabilize dashboard progress.
  Required behavior: the top progress card represents the whole active sync cycle, not only the current file; progress does not reset on every file; speed and ETA use smoothed global samples and do not flicker; many-small-file sync still shows useful global file-rate/ETA.
  Current status: global file-rate smoothing is implemented, run-level byte and file remaining-time predictions now dampen raw ETA changes instead of recomputing jumpy estimates on every sample, and the refreshed Linux visual-smoke progress screen shows aggregate multi-folder progress; keep open for animated Windows verification and any remaining global-progress corrections.
  Partial 2026-06-07: refreshed Linux visual-smoke from a fresh desktop publish at `/tmp/cotton-sync-linux-walkthrough-20260607-r2`; the progress screen shows `Syncing 2 folders`, aggregate file count, transfer count, right-aligned size/speed, and per-folder active rows without clipping.
  Verification: focused view-model tests plus Windows manual check with many small files and at least one large file.
- [ ] Fix folder-list usability.
  Required behavior: folder rows use compact status dots/icons instead of dangling status text, expanded row actions remain reachable in a small window, the folder list scrolls independently, and Activity does not consume the primary folder-management area unless explicitly opened.
  Current status: layout-side fixes are implemented, and the refreshed Linux visual-smoke folder-controls screen shows the expanded editor reachable with Activity hidden; keep open for Windows visual verification with multiple sync pairs.
  Partial 2026-06-07: refreshed Linux visual-smoke from a fresh desktop publish at `/tmp/cotton-sync-linux-walkthrough-20260607-r2`; folder rows use status dots, compact folder/menu buttons, an independently sized Folders area, and the expanded editor remains reachable without Activity consuming the panel.
  Verification: visual check on Linux for layout regressions and Windows manual check with two or more sync pairs.
- [ ] Fix startup and authentication polish.
  Required behavior: saved sessions show a connecting/restoring state instead of the login form, login supports Enter, and auth errors stay human-readable without moving the whole form offscreen.
  Current status: code-side audit is done; keep open for Windows restart/sign-in verification.
  Verification: focused view-model tests plus Windows manual restart/sign-in check.
- [ ] Fix desktop notification and tray polish.
  Required behavior: tray icon is the normal Cotton icon when idle, changes only for syncing/paused/error states, Windows notifications use the product name and icon where the platform API allows it, and key desktop app events notify outside the web app too.
  Current status: code-side tray/notification audit is done. Tray unavailable actions are now removed from the native menu instead of relying on disabled/hidden menu items, so Windows native menu rendering should not show a disabled `Pause sync` entry when pause/resume is unavailable. Windows installed-app identity and visual notification check remain in Windows-only work.
  Partial 2026-06-07: `DesktopTrayController` now rebuilds the native tray menu from available actions and does not set disabled action items. Focused tray/menu verification passed 15/15 after a clean desktop build.
  Verification: Linux smoke where supported and Windows manual notification/tray check.
- [x] Re-run a minimal desktop verification pass.
  Required behavior: avoid full-suite churn for every tiny step; run focused tests for the changed behavior, then desktop Release build before commit.
  Current status: completed for this pass with focused desktop tests and desktop build.
  Verification: record exact commands and results before marking done.
  Verification result: `dotnet test src/Cotton.Sync.Desktop.Tests/Cotton.Sync.Desktop.Tests.csproj --no-restore --filter "RunProgressChanged|TransferProgressChanged|DesktopSetupVisualContractTests|ShowSelectedSyncPairEditorCommand|ToggleActivityCommand|InitializeAsync_ShowsStartupLoadingInsteadOfSetupWhileRestoringSession|SignInCommand_ShowsHuman|SignInInputs_SubmitOnEnterAndReturnKeys|DesktopTrayStatusResolverTests|DesktopTrayMenuContractTests|DesktopNotificationServiceFactoryTests"` passed 94/94; `dotnet build src/Cotton.Sync.Desktop/Cotton.Sync.Desktop.csproj --no-restore` passed with 0 warnings and 0 errors.
