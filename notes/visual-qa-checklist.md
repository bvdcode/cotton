# Cotton Sync Desktop Visual QA Checklist

Use this checklist for Avalonia desktop visual review. It replaces the older web/MUI checklist, which was not suitable for the compact tray-style Cotton Sync Desktop window.

Do not mark the release UI screenshot review complete from this checklist alone. The release gate still requires screenshots and manual walkthroughs on Linux and Windows.

## Capture Set

- [ ] Setup server step at minimum window size.
- [ ] Setup sign-in step at minimum window size.
- [ ] First-run add-folder wizard: local folder step.
- [ ] First-run add-folder wizard: cloud folder browser with folders.
- [ ] First-run add-folder wizard: empty cloud folder.
- [ ] Dashboard with no sync folders.
- [ ] Dashboard with one sync folder.
- [ ] Dashboard with multiple sync folders.
- [ ] Dashboard while syncing.
- [ ] Dashboard while paused.
- [ ] Dashboard offline state.
- [ ] Dashboard action-required state.
- [ ] Dashboard conflict state.
- [ ] Settings Account tab.
- [ ] Settings Startup tab.
- [ ] Settings Prefs tab.
- [ ] Settings Diagnostics tab with self-test results.
- [ ] Diagnostics export success state.
- [ ] Tray menu on Windows.
- [ ] Taskbar/window icon on Windows.
- [ ] Linux startup/autostart behavior on the tested desktop environment.

## Layout Rules

- [ ] Window opens centered on first launch and after profile changes.
- [ ] Minimum setup window fits all controls without clipped buttons or fields.
- [ ] Minimum dashboard window fits status, folders, and activity without overlapping controls.
- [ ] Settings fits within the dashboard window; tab headers remain reachable.
- [ ] Add-folder wizard fits within the dashboard window and does not hide the primary action.
- [ ] Text buttons and icon buttons have consistent height in shared rows.
- [ ] Long server URLs, usernames, local paths, and remote paths truncate cleanly.
- [ ] Action-required and conflict messages truncate without pushing buttons off-screen.
- [ ] No card is nested inside another decorative card.
- [ ] No text overlaps preceding or following content.

## Interaction Rules

- [ ] Server entry starts empty on a fresh profile.
- [ ] Server verification shows a clear success/failure state.
- [ ] Wrong password, missing TOTP, and invalid TOTP show human-readable errors on the sign-in form.
- [ ] Sign-in with no sync folders opens the add-folder wizard.
- [ ] Add folder opens the native local folder picker before cloud selection.
- [ ] Cloud folder navigation supports parent/open actions and double-click open.
- [ ] Existing sync folders expose edit name, save, enable/disable, open, and remove actions on the dashboard.
- [ ] Pause and resume are mutually exclusive in menus.
- [ ] Sync now is hidden while paused, busy, or unavailable.
- [ ] Settings closes without changing unrelated state.
- [ ] Sign out clears password/TOTP fields and closes transient overlays.

## Visual Quality

- [ ] Brand icon is visible and crisp in setup, dashboard, tray, and taskbar where supported.
- [ ] The lime accent is used deliberately and does not dominate the whole UI.
- [ ] Light and dark themes both have readable contrast.
- [ ] Unavailable actions are hidden when they would only add clutter; intentionally disabled preview controls still look disabled, not broken.
- [ ] Status colors are consistent: healthy, paused, offline, error.
- [ ] Empty states are useful but compact.
- [ ] Diagnostics and self-test rows are scannable.
- [ ] Folder rows are dense enough for repeated use but still touch-friendly.
- [ ] Tooltips explain icon-only actions.

## Platform Notes

- [ ] Windows tray: close-to-tray, show app, sync now, pause/resume, settings, quit.
- [ ] Windows autostart: enabled setting survives reboot and launches the app.
- [ ] Windows notifications: initial sync, conflict, and action-required notifications render.
- [ ] Windows secure storage: DPAPI token roundtrip and logout clear behavior verified.
- [ ] Linux autostart: XDG `.desktop` entry launches after login.
- [ ] Linux notifications: `notify-send` adapter works where supported.
- [ ] Linux tray limitation is visible and accurate for the tested desktop environment.
- [ ] Linux secure storage: Secret Service token roundtrip and logout clear behavior verified.

## Evidence

- [ ] Record OS, desktop environment, display scaling, and build artifact tested.
- [ ] Save screenshot paths in `notes/sdk-desktop-sync-client-plan.md`.
- [ ] Record commands used for headless captures or manual walkthroughs.
- [ ] Record failures as follow-up tasks before marking Phase 8 visual QA complete.
