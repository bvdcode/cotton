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

- `[x]` means the sub-plan is closed and should not be opened during normal work.
- `[ ]` means the sub-plan still has open release work, manual verification, reference guardrails, or future scope.

Closed implementation sub-plans after this audit: Phase 1, Phase 2, Phase 3, Phase 4, and Phase 5. They are done unless a new bug explicitly points back to one of them.

Highest-priority active work lives in `00-current-work-order.md`. It is the shortest queue for the current desktop polish pass: sync controls, dashboard progress, folder-list usability, startup/auth polish, and tray/notification polish. Most of those are now code-side audited and waiting for Windows/manual verification, so do not repeatedly reopen completed phase files unless a new bug points there.

Release-grade remaining work is mostly verification, not broad architecture:

- Windows/manual gates: Windows shell identity, tray behavior, notifications, secure storage, and large-tree/large-file desktop smoke.
- Clean-machine gates: Windows installer/archive, Linux package/archive, uninstall, and upgrade.
- Soak/performance gates: continuous sync soak, crash recovery, disk-full and permission-denied action-required checks, many-file stability, and 24-hour one/two-client soak.
- Final release gates: full Release build/test pass, screenshot review, diagnostics export verification, release notes, checksums, and final branch diff review.

Current code-side horizon:

- Continue from `00-current-work-order.md` first.
- Then finish active Phase 8 polish that can be validated on Linux.
- Then defer Windows-only, clean-machine, and long-running soak items to their manual-gated files.

Do not spend time in `future` or `reference` sub-plans unless the user explicitly promotes that work.

## Execution Buckets

Revision date: 2026-06-07.

The audit counted implementation checkboxes in every linked sub-plan. Closed files are marked with `[x]` in `Plan Files` and should not be opened again during normal work.

Active code-side queue:

- `00-current-work-order.md`: the immediate desktop polish queue. This is the first file to use when deciding the next implementation step.
- `18-phase-8-desktop-ux-and-visual-design.md`: open only for Linux-verifiable desktop visual/layout polish not already captured by the current work order.
- `22-phase-12-end-to-end-test-matrix.md`: open only for focused edge-case coverage tied to a concrete bug or release gate.
- `23-phase-13-performance-and-soak.md`: open only for performance fixes, scale safety, and measured desktop sync behavior.

Manual-gated queue:

- `01-windows-only-work.md`
- `16-phase-6-continuous-sync.md`
- `17-phase-7-authentication-and-token-storage.md`
- `19-phase-9-tray-autostart-notifications-and-lifecycle.md`
- `20-phase-10-diagnostics-and-supportability.md`
- `21-phase-11-packaging-and-installers.md`
- `24-phase-14-release-readiness-gate.md`

Reference and future files are not execution queues. They stay unchecked because they contain product guardrails or intentionally deferred scope, not because they should be worked top-to-bottom now.

## Plan Files

The release plan is split into focused files so the active checklist stays visible and reviewable. Keep current execution work in `00-current-work-order.md`; keep platform-specific Windows tasks in `01-windows-only-work.md`.

- [ ] [Current Work Order](sdk-desktop-sync-client-plan/00-current-work-order.md) - active, 5 open, 1 done
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
- [ ] [Phase 7 - Authentication And Token Storage](sdk-desktop-sync-client-plan/17-phase-7-authentication-and-token-storage.md) - manual-gated secure-storage checks, 2 open, 10 done
- [ ] [Phase 8 - Desktop UX And Visual Design](sdk-desktop-sync-client-plan/18-phase-8-desktop-ux-and-visual-design.md) - active/manual-gated visual polish, 8 open, 22 done
- [ ] [Phase 9 - Tray, Autostart, Notifications, And Lifecycle](sdk-desktop-sync-client-plan/19-phase-9-tray-autostart-notifications-and-lifecycle.md) - manual-gated platform polish, 5 open, 12 done
- [ ] [Phase 10 - Diagnostics And Supportability](sdk-desktop-sync-client-plan/20-phase-10-diagnostics-and-supportability.md) - manual-gated diagnostics export, 1 open, 7 done
- [ ] [Phase 11 - Packaging And Installers](sdk-desktop-sync-client-plan/21-phase-11-packaging-and-installers.md) - manual-gated clean-machine packaging, 5 open, 9 done
- [ ] [Phase 12 - End-To-End Test Matrix](sdk-desktop-sync-client-plan/22-phase-12-end-to-end-test-matrix.md) - active/manual-gated edge cases, 3 open, 21 done
- [ ] [Phase 13 - Performance And Soak](sdk-desktop-sync-client-plan/23-phase-13-performance-and-soak.md) - active/manual-gated performance and soak, 5 open, 8 done
- [ ] [Phase 14 - Release Readiness Gate](sdk-desktop-sync-client-plan/24-phase-14-release-readiness-gate.md) - final release gate, 20 open
- [ ] [Future Platform Features](sdk-desktop-sync-client-plan/30-future-platform-features.md) - future, not release-blocking, 10 open
- [ ] [Working Rules For This Plan](sdk-desktop-sync-client-plan/99-working-rules-for-this-plan.md) - reference operating rules, 6 open
