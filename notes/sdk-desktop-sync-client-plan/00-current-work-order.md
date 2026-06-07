## Current Work Order

Use this section as the active checklist for the current desktop-client pass. Do not jump to a lower item while a higher item has an unresolved correctness or usability blocker.

- [ ] Stabilize sync controls during active work.
  Required behavior: global pause/resume is available while synchronization is running, disabling one pair does not resume other pairs after a global pause, and paused state survives application restart.
  Current status: code-side command/state audit is done; keep open for Windows two-pair manual verification.
  Verification: focused command/state tests plus Windows manual check with two sync pairs.
- [ ] Stabilize dashboard progress.
  Required behavior: the top progress card represents the whole active sync cycle, not only the current file; progress does not reset on every file; speed and ETA use smoothed global samples and do not flicker; many-small-file sync still shows useful global file-rate/ETA.
  Current status: global file-rate smoothing is implemented; keep open for visual Windows verification and any remaining global-progress corrections.
  Verification: focused view-model tests plus Windows manual check with many small files and at least one large file.
- [ ] Fix folder-list usability.
  Required behavior: folder rows use compact status dots/icons instead of dangling status text, expanded row actions remain reachable in a small window, the folder list scrolls independently, and Activity does not consume the primary folder-management area unless explicitly opened.
  Current status: layout-side fixes are implemented; keep open for Windows visual verification with multiple sync pairs.
  Verification: visual check on Linux for layout regressions and Windows manual check with two or more sync pairs.
- [ ] Fix startup and authentication polish.
  Required behavior: saved sessions show a connecting/restoring state instead of the login form, login supports Enter, and auth errors stay human-readable without moving the whole form offscreen.
  Current status: code-side audit is done; keep open for Windows restart/sign-in verification.
  Verification: focused view-model tests plus Windows manual restart/sign-in check.
- [ ] Fix desktop notification and tray polish.
  Required behavior: tray icon is the normal Cotton icon when idle, changes only for syncing/paused/error states, Windows notifications use the product name and icon where the platform API allows it, and key desktop app events notify outside the web app too.
  Verification: Linux smoke where supported and Windows manual notification/tray check.
- [ ] Re-run a minimal desktop verification pass.
  Required behavior: avoid full-suite churn for every tiny step; run focused tests for the changed behavior, then desktop Release build before commit.
  Verification: record exact commands and results before marking done.
