# Cotton Sync Desktop Release Plan

This is the ground-up release plan for the Cotton Cloud desktop synchronization client. It replaces the previous prototype/foundation plan and should be treated as the source of truth for building a real releasable application, not a demo.

All implementation checkboxes start unchecked. A task can be marked done only after its verification item is completed and recorded in this file or in the linked task notes.

## Status Key

- `complete`: the sub-plan has no open implementation checkboxes and does not need to be opened again unless new scope is added.
- `active`: current implementation or verification work can still move forward on the current branch.
- `manual-gated`: code-side work is mostly done, but the item needs Windows, clean-machine, installed-package, or long-running manual verification.
- `reference`: product/architecture/working-rule guardrails. Keep them visible, but do not treat them as the next implementation queue.
- `future`: intentionally outside the current release unless explicitly promoted.

## Current Horizon

Audit date: 2026-06-07.

Use the checkbox in the `Plan Files` section as the sub-plan reopen flag:

- `[x]` means the sub-plan is fully closed and should not be opened during normal work.
- `[ ]` means the sub-plan still has open release work, manual verification, reference guardrails, or future scope.

Mechanical audit 2026-06-07:

- Command used: `for f in notes/sdk-desktop-sync-client-plan/*.md; do open=$(rg -c '^[-*] \[ \]' "$f" || true); done=$(rg -c '^[-*] \[[xX]\]' "$f" || true); printf '%s|open=%s|done=%s\n' "$f" "$open" "$done"; done`
- Result: the `Plan Files` counts below match the actual sub-plan checkboxes.
- Fully closed files are only the five `[x]` implementation phases below. Treat any other file as open until another mechanical audit proves it has zero open release or manual-gate checkboxes.
- Revision pass 2026-06-07: rechecked the same ledger with `rg -c '^[-*] \[ \]'` and `rg -c '^[-*] \[[xX]\]'` across the main plan and every sub-plan. No new sub-plan qualifies for `[x]`.

Latest audit result: no additional sub-plan can be marked fully complete after the current desktop pass. Phase 1 through Phase 5 remain the only fully closed implementation sub-plans. Every other unchecked file still has at least one open release gate, manual gate, reference guardrail, or future item.

Revision 2026-06-07: re-ran the sub-plan checkbox audit for this pass. The `Plan Files` checklist remains the closure ledger: `[x]` files are closed and should stay out of the normal work horizon; `[ ]` files are either active, manual-gated, reference, or future scope and must not be treated as complete.

Closed implementation sub-plans after this audit:

- Phase 1 - Release-Grade App Model.
- Phase 2 - Application Layer.
- Phase 3 - Backend Sync Change Feed.
- Phase 4 - Optimistic Concurrency And Safe Remote Operations.
- Phase 5 - Sync Core Hardening.

These closed sub-plans are done. Do not reopen them during normal work unless a new concrete bug points directly back to one of them.

## Closure Ledger Rules

The `Plan Files` checklist is the closure ledger for the whole desktop plan.

- Before opening a sub-plan, check its `Plan Files` row in this file.
- If the row is `[x]`, treat that sub-plan as complete and do not inspect it during normal horizon checks.
- If the row is `[ ]`, use its status text to decide whether it belongs to the active queue, the manual-gated queue, reference guardrails, or future work.
- Mark a sub-plan `[x]` only when it has zero open release-work checkboxes and its required verification evidence is recorded in that sub-plan.
- Do not mark manual-gated Windows, clean-machine, packaging, soak, diagnostics, or release-readiness sub-plans complete from Linux-only evidence.
- When a sub-plan becomes complete, update the row in `Plan Files`, add it to the closed sub-plan list above, and refresh the counts in `Revision Result`.

## Sub-Plan Opening Rules

Open plan files in this order:

1. `00-current-work-order.md` for active desktop-client bugs and polish.
2. `17-phase-7-authentication-and-token-storage.md` when working on the promoted app-code browser login integration or secure-storage verification.
3. `18-phase-8-desktop-ux-and-visual-design.md` only when the current work order points to visual/layout/manual screenshot work.
4. `22-phase-12-end-to-end-test-matrix.md` only when a concrete end-to-end edge case is being verified or fixed.
5. `23-phase-13-performance-and-soak.md` only when working on scale, throughput, memory, UI responsiveness, or soak behavior.
6. Manual-gated files only when the matching platform or clean-machine environment is available.

Do not open `[x]` sub-plans during normal work. Do not use `reference` or `future` files as implementation queues unless the user explicitly promotes one of those items.

Primary active queue:

- `00-current-work-order.md` remains the first file to open. It has 6 open items and 0 completed verification items because remote branch alignment was reopened after the 2026-06-09 `origin/develop` audit.
- The open items are mostly code-side audited, but they are not complete until the required Windows/manual checks pass or a new implementation bug is fixed.
- Browser-login integration is now release scope because `develop` added `/api/v1/oauth/app-code` start/approval/poll endpoints; use Phase 7 for the SDK, app-layer, CLI, and desktop implementation details.
- Do not jump into lower-priority files while an item in this work order has an unresolved correctness or usability blocker.

Current implementation horizon:

- Finish Linux-verifiable desktop polish only when it directly closes an open `00-current-work-order.md` item.
- Implement the app-code browser login path across SDK, app layer, CLI, and desktop before treating authentication as release-ready.
- Use Phase 8 only for remaining visual/layout polish and screenshot/manual walkthrough gates.
- Use Phase 12 only for concrete end-to-end edge cases: process crash recovery, real disk-full behavior, and Windows permission-denied behavior.
- Use Phase 13 only for scale/performance safety: many-file stability, Windows desktop large-file smoke, 24-hour soak, and measured resource tracking.

Manual and release horizon:

- Windows/manual gates: Windows shell identity, tray behavior, notifications, secure storage, and large-tree/large-file desktop smoke.
- Clean-machine gates: Windows installer/archive, Linux package/archive, uninstall, and upgrade.
- Soak/performance gates: continuous sync soak, crash recovery, disk-full and permission-denied action-required checks, many-file stability, and 24-hour one/two-client soak.
- Final release gates: full Release build/test pass, screenshot review, diagnostics export verification, release notes, checksums, and final branch diff review.

Do not spend time in `future` or `reference` sub-plans unless the user explicitly promotes that work.

## Execution Buckets

Revision date: 2026-06-07.

The audit counted implementation checkboxes in every linked sub-plan. Closed files are marked with `[x]` in `Plan Files` and should not be opened again during normal work.

Active code-side queue:

- `00-current-work-order.md`: immediate desktop polish queue and the first source for the next implementation step.
- `17-phase-7-authentication-and-token-storage.md`: active again for app-code browser login integration across SDK, app layer, CLI, and desktop; secure-storage verification remains manual-gated.
- `18-phase-8-desktop-ux-and-visual-design.md`: open only for desktop visual/layout polish, screenshot review, or manual walkthrough work not already captured by the current work order.
- `22-phase-12-end-to-end-test-matrix.md`: open only for focused edge-case coverage tied to a concrete bug or release gate.
- `23-phase-13-performance-and-soak.md`: open only for performance fixes, scale safety, and measured desktop sync behavior.

Manual-gated queue:

- `01-windows-only-work.md`: Windows-only shell, tray, virtual-files, and large-tree/large-file work. Do not implement Windows-specific features from Linux unless the item is only plan/research.
- `16-phase-6-continuous-sync.md`: 2-hour continuous sync soak remains.
- `17-phase-7-authentication-and-token-storage.md`: Windows/Linux secure-storage manual verification remains after the browser-login code path is implemented.
- `19-phase-9-tray-autostart-notifications-and-lifecycle.md`: installed-app tray/autostart/notification verification remains.
- `20-phase-10-diagnostics-and-supportability.md`: manual diagnostics export verification remains.
- `21-phase-11-packaging-and-installers.md`: clean-machine install, uninstall, portable archive, Linux package/archive, and upgrade checks remain.
- `24-phase-14-release-readiness-gate.md`: final release gate; only use after active/manual gates above are complete.

Reference and future files are not execution queues. They stay unchecked because they contain product guardrails or intentionally deferred scope, not because they should be worked top-to-bottom now.

## Revision Result

Revision date: 2026-06-07.

- Fully complete: 5 implementation sub-plans.
- Newly closed by this audit: 0 sub-plans.
- Current desktop-polish queue: 6 open, 0 done.
- Active/manual authentication: Phase 7 has 6 open, 10 done.
- Active/manual visual polish: Phase 8 has 7 open, 23 done.
- Active/manual end-to-end edge cases: Phase 12 has 3 open, 21 done.
- Active/manual performance and soak: Phase 13 has 5 open, 8 done.
- Manual platform, diagnostics, packaging, and release gates remain open by design.
- Reference and future files remain unchecked by design and are not part of the immediate work queue.

Immediate next horizon:

- Close `00-current-work-order.md` items only when their Windows/manual checks are complete or a concrete implementation bug is fixed. When all current-work-order items are closed or moved into their owning phase file, mark the work-order row `[x]`.
- Use Linux only for code-side desktop checks, visual-smoke screenshots, and non-Windows logic. Do not start Windows-only implementation work from Linux.
- Keep backend/server changes out of this desktop branch unless the user explicitly asks for a reviewed backend change.

Current platform boundary:

- The Linux branch can still support code audits, focused tests, visual-smoke screenshots, packaging scripts, and sync-core/app-layer performance work.
- The current work order cannot be fully closed from Linux alone. Its remaining closure evidence is mostly animated Windows desktop behavior: two-pair pause/disable/restart, tray menu availability while syncing, notification identity/icon rendering, compact folder-list interaction, and large-file/many-small-file progress stability.
- When a Windows agent/session is available, open `01-windows-only-work.md` first and verify those platform gates before changing code. If a Windows-only failure is found, make the smallest Windows/platform-scoped fix and record the evidence in the matching sub-plan.

## Plan Files

The release plan is split into focused files so the active checklist stays visible and reviewable. Keep current execution work in `00-current-work-order.md`; keep platform-specific Windows tasks in `01-windows-only-work.md`.

- [ ] [Current Work Order](sdk-desktop-sync-client-plan/00-current-work-order.md) - active, 6 open, 0 done
- [ ] [Windows-Only Work](sdk-desktop-sync-client-plan/01-windows-only-work.md) - manual-gated, 4 open
- [ ] [Product Target](sdk-desktop-sync-client-plan/02-product-target.md) - reference release outcome, 11 open
- [ ] [Non-Negotiable Decisions](sdk-desktop-sync-client-plan/03-non-negotiable-decisions.md) - reference guardrails, 9 open, 1 done
- [ ] [Architecture Overview](sdk-desktop-sync-client-plan/04-architecture-overview.md) - reference architecture map, 5 open, 1 done
- [ ] [Phase 0 - Ground Truth And Branch Hygiene](sdk-desktop-sync-client-plan/10-phase-0-ground-truth-and-branch-hygiene.md) - reference historical audit, 4 open, 1 done
- [x] [Phase 1 - Release-Grade App Model](sdk-desktop-sync-client-plan/11-phase-1-release-grade-app-model.md) - complete, 9 done
- [x] [Phase 2 - Application Layer](sdk-desktop-sync-client-plan/12-phase-2-application-layer.md) - complete, 12 done
- [x] [Phase 3 - Backend Sync Change Feed](sdk-desktop-sync-client-plan/13-phase-3-backend-sync-change-feed.md) - complete, 11 done
- [x] [Phase 4 - Optimistic Concurrency And Safe Remote Operations](sdk-desktop-sync-client-plan/14-phase-4-optimistic-concurrency-and-safe-remote-operations.md) - complete, 10 done
- [x] [Phase 5 - Sync Core Hardening](sdk-desktop-sync-client-plan/15-phase-5-sync-core-hardening.md) - complete, 27 done
- [ ] [Phase 6 - Continuous Sync](sdk-desktop-sync-client-plan/16-phase-6-continuous-sync.md) - manual-gated soak, 1 open, 14 done
- [ ] [Phase 7 - Authentication And Token Storage](sdk-desktop-sync-client-plan/17-phase-7-authentication-and-token-storage.md) - active app-code browser login plus manual secure-storage checks, 6 open, 10 done
- [ ] [Phase 8 - Desktop UX And Visual Design](sdk-desktop-sync-client-plan/18-phase-8-desktop-ux-and-visual-design.md) - active/manual-gated visual polish, 7 open, 23 done
- [ ] [Phase 9 - Tray, Autostart, Notifications, And Lifecycle](sdk-desktop-sync-client-plan/19-phase-9-tray-autostart-notifications-and-lifecycle.md) - manual-gated platform polish, 5 open, 12 done
- [ ] [Phase 10 - Diagnostics And Supportability](sdk-desktop-sync-client-plan/20-phase-10-diagnostics-and-supportability.md) - manual-gated diagnostics export, 1 open, 7 done
- [ ] [Phase 11 - Packaging And Installers](sdk-desktop-sync-client-plan/21-phase-11-packaging-and-installers.md) - manual-gated clean-machine packaging, 5 open, 9 done
- [ ] [Phase 12 - End-To-End Test Matrix](sdk-desktop-sync-client-plan/22-phase-12-end-to-end-test-matrix.md) - active/manual-gated edge cases, 3 open, 21 done
- [ ] [Phase 13 - Performance And Soak](sdk-desktop-sync-client-plan/23-phase-13-performance-and-soak.md) - active/manual-gated performance and soak, 5 open, 8 done
- [ ] [Phase 14 - Release Readiness Gate](sdk-desktop-sync-client-plan/24-phase-14-release-readiness-gate.md) - final release gate, 20 open
- [ ] [Future Platform Features](sdk-desktop-sync-client-plan/30-future-platform-features.md) - future, not release-blocking, 10 open
- [ ] [Working Rules For This Plan](sdk-desktop-sync-client-plan/99-working-rules-for-this-plan.md) - reference operating rules, 7 open
