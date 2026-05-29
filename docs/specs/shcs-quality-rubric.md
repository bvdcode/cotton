# SHCS — A Quality Rubric for Self-Hosted Cloud Storage

**Author:** Vadim Belov

**Intended audience:** authors and operators of self-hosted cloud storage systems

---

## 0. What this is — and what it is not

This is an **opinionated rubric**, not a neutral industry standard.

It is written by someone who builds a self-hosted cloud storage system, and it
reflects a specific point of view about what "good" looks like. The point of view
is defensible and the reasoning is given throughout — but you should read this as
**an argument, not a vendor-neutral specification**. Where a system built to these
opinions scores well, that is because it was built to these opinions, not the
other way around.

We deliberately drop the trappings of a formal standard (RFC-style normative
keywords, a numbered series, a registry of certified declarations). Those create
a false impression of neutrality and authority that a single-author document has
not earned. What remains is a sharp, honest framework for **comparing systems and
articulating a quality bar** — useful exactly to the extent that you find the
arguments convincing.

If this framework is ever going to become a real shared standard, that requires
multiple independent authors and external review. That is social work, not
editing, and it has not happened yet.

---

## 1. Purpose and Scope

The goal is:

- to give **technical buyers and admins** a way to compare systems beyond
  marketing checklists ("has WebDAV", "has mobile app");
- to give **authors of new systems** a concrete target for what _good_ looks like
  in architecture, reliability, security and operations;
- to give a **shared vocabulary** (axes and classes) for talking about how
  "industrial" a self-hosted cloud storage system really is.

The rubric is about:

- **file and object storage** served over a network;
- **self-hosted deployments**, where the operator runs the server components.

It is **not** about:

- SaaS-only products;
- backup-only tools (e.g. pure rsync wrappers);
- single-user sync tools without multi-user/cloud semantics.

---

## 2. Terminology

- **SHCS** — Self-Hosted Cloud Storage; here, both the rubric and the class of
  systems it evaluates.
- **System** — a particular self-hosted cloud storage software stack (server and
  its official ecosystem).
- **Operator** — the person or team running the system on their own infrastructure.
- **Client** — any official desktop/mobile/web/sync client or protocol endpoint
  (e.g. WebDAV, S3, etc.).
- **User** — end-user who stores and accesses data through the system.

Scoring words ("must", "should") are used in their plain-English sense, not as
formal normative keywords.

---

## 3. What qualifies as an SHCS system

This rubric applies to systems that meet **all** of the following:

1. **Self-hosted capability**
   The server software can be run by an operator on their own infrastructure
   (VM, bare metal, container, etc.).

2. **Multi-user cloud semantics**
   The system provides:
   - user accounts and authentication;
   - remote access via browser and/or protocols (WebDAV/S3-compatible/other).

3. **Persistent file/object storage**
   The system stores user data durably on disk or other persistent media, not
   merely in memory or as a temporary cache.

4. **File/folder-like abstraction for users**
   Users see their data via some form of hierarchy (folders, collections,
   libraries, etc.), not only as opaque IDs.

If any of these are missing, the software is **out of scope** and is not scored.

---

## 4. Evaluation Axes

Each in-scope system is evaluated along **seven axes**:

1. **SHCS-ARCH** — Architecture & Storage Model
2. **SHCS-PERF** — Performance & Scaling
3. **SHCS-DATA** — Data Integrity & Cryptography
4. **SHCS-SEC** — Security & Access Control
5. **SHCS-UX** — Predictability & User Experience
6. **SHCS-OPS** — Operations, Deployment & Upgrades
7. **SHCS-ECO** — Protocols, Clients & Ecosystem

Each axis is scored from **0 to 3**:

- **3 — Industrial-grade**
- **2 — Solid / acceptable**
- **1 — Marginal / fragile**
- **0 — Not acceptable** (for anything calling itself "cloud storage")

The criteria below are illustrative, not exhaustive. When evidence is mixed or you
are unsure between two levels, **round down** (see Section 6).

---

### 4.1 SHCS-ARCH — Architecture & Storage Model

How the system is structured internally, and whether storage is a first-class,
robust concept. This axis is about the **properties** of the storage model, not
any particular implementation; content-addressing, files-plus-metadata, object
stores and others can all qualify if the properties hold.

**Score 3 (Industrial-grade)** — system:

- Has a **clear, explicitly documented storage model** with stable, stated
  invariants (e.g. "this ID is immutable", "this mapping is always consistent"),
  whatever the concrete representation.
- Treats storage as a **first-class engine**, with the UI and APIs as clients of
  that engine.
- Uses a runtime and architecture that support concurrency, streaming I/O and
  long-term maintainability.

**Score 2 (Solid)** — system:

- Has a reasonably clear storage model and sticks to it.
- UI and storage are somewhat coupled, but not hopelessly entangled.
- Architecture allows streaming and partial reads, even if not consistently used.

**Score 1 (Marginal)** — system:

- Is effectively a **thin wrapper over a filesystem**, with weak or ad-hoc
  modeling of metadata/content.
- UI/business logic heavily entangled with storage; changing one risks breaking
  the other.
- Lifetime/consistency guarantees are unclear or implicit.

**Score 0 (Not acceptable)** — system:

- Has no coherent storage model (ad-hoc paths, sidecar files, undocumented
  invariants).
- Depends on fragile, legacy architectural choices with no realistic path to fix.

---

### 4.2 SHCS-PERF — Performance & Scaling

How the system behaves under load, and whether it was designed for realistic and
worst-case scenarios. This axis is about the **engine**: throughput, scaling and
stability under load. (Whether the UI _communicates_ that behavior predictably is
scored separately under SHCS-UX.)

**Score 3 (Industrial-grade)**:

- Demonstrably supports:
  - large directories / graphs (e.g. **100k–1M** items) without UI or API
    collapse;
  - large, continuous transfers (multi-GB) **without degrading** to "hundreds of
    KB/s".
- Performance is **stable over time**: a transfer that starts fast stays fast
  until completion, assuming stable network and hardware.
- Worst-case scenarios (very large trees, many small files, long-lived streams)
  are explicitly considered and remain correct and usable.

**Score 2 (Solid)**:

- Comfortably handles tens of thousands of items per directory and multi-GB
  transfers with minor or occasional slowdowns.
- Some operations may degrade under extreme load, but without corrupting metadata
  or hanging indefinitely.

**Score 1 (Marginal)**:

- Performs acceptably for small/medium workloads (a few thousand files,
  occasional large uploads).
- Under higher loads it frequently degrades to very low throughput, blocks for
  long periods, or requires manual tuning to stay usable.

**Score 0 (Not acceptable)**:

- Performance issues routinely make core operations (upload, list, browse)
  unusable under common conditions.
- The system cannot reliably complete large uploads or handle moderately large
  trees.

---

### 4.3 SHCS-DATA — Data Integrity & Cryptography

How seriously the system treats the safety of stored data.

Cryptography is scored **conditionally**: a system that does not claim to offer
encryption is not penalized for its absence on this axis — it is judged purely on
integrity. A system that _does_ offer encryption is judged on whether that
encryption is correct and fail-safe.

**Score 3 (Industrial-grade)**:

- Data integrity is actively verified: content hashes/checksums computed and
  stored; optional or periodic **sanity checks** that detect disk corruption or
  bitrot.
- Partial failures (e.g. interrupted migration) do **not silently destroy or
  orphan data**; failures are fail-safe and well-signaled.
- _If encryption is offered_, it is integrated into the storage model, designed
  with correct nonce/key semantics, and implemented using robust libraries.

**Score 2 (Solid)**:

- Basic integrity protection exists: checksums/hashes at rest or during transfer;
  detection of mismatched content in common paths.
- Failure modes may render data inaccessible only under uncommon conditions, and
  usually with operator-visible errors.
- _If encryption is offered_, it uses standard building blocks in a conventional
  way, even if not deeply integrated into the model.

**Score 1 (Marginal)**:

- Integrity is mostly delegated to the underlying filesystem/RAID; the
  application detects little itself.
- Operators must rely heavily on external tooling (e.g. ZFS scrub) to detect
  corruption.
- _If encryption is offered_, it is a bolt-on feature layer with documented
  failure modes that include data loss or confusion in partially completed
  operations.

**Score 0 (Not acceptable)**:

- A history or design where migrations — or an offered encryption feature —
  **routinely and silently** corrupt or lose user data.
- No meaningful integrity checking at the application level.

---

### 4.4 SHCS-SEC — Security & Access Control

How well the system protects data from the people who should not reach it. This is
distinct from SHCS-DATA: a system can keep bytes perfectly intact and still leak
them to the wrong user. For multi-user cloud storage, access-control correctness
is a first-class safety property.

**Score 3 (Industrial-grade)**:

- Authentication is robust and modern (e.g. strong password handling, support for
  SSO/OIDC, and MFA-capable where appropriate).
- Authorization enforces least privilege; sharing and permission models are
  correct and hard to misconfigure into accidental exposure.
- **Multi-tenant isolation** is correct: one user/tenant cannot reach another's
  data, even under concurrency or edge cases.
- Security-relevant events are auditable; secrets are handled sanely; defaults are
  secure out of the box; a threat model is documented.

**Score 2 (Solid)**:

- Solid authentication and working authorization; sharing/permissions behave
  correctly in common cases.
- Isolation is correct in normal use; defaults are reasonable; basic audit
  logging exists. Some hardening gaps remain.

**Score 1 (Marginal)**:

- Authentication exists but authorization is coarse; the sharing/permission model
  is fragile or easily misconfigured into exposure.
- Isolation is weak in edge cases; auditing is minimal; defaults require manual
  hardening to be safe.

**Score 0 (Not acceptable)**:

- Broken access control: privilege escalation, cross-tenant data exposure, or
  sharing that routinely leaks data; no meaningful authorization.

---

### 4.5 SHCS-UX — Predictability & User Experience

Not about "prettiness" but about **predictability and responsiveness** — how the
system communicates and behaves for the user, given the engine's performance.

**Score 3 (Industrial-grade)**:

- Core actions have predictable outcomes: uploading, renaming, deleting, moving
  do exactly what the user expects; no hidden heuristics "clean up" or "optimize"
  data behind the user's back.
- The web UI stays responsive even for large trees: virtualized lists, incremental
  loading, clear state feedback.
- Long-running operations clearly communicate progress, errors and eventual
  success/failure.

**Score 2 (Solid)**:

- UI is generally usable and predictable under normal loads.
- Some operations may become sluggish on very large trees or under heavy
  concurrency, but remain understandable and controllable.

**Score 1 (Marginal)**:

- UI frequently blocks or stalls for multiple seconds on everyday operations.
- Users encounter non-obvious behaviors (e.g. files disappearing due to space
  constraints or sync conflicts) that are not clearly explained.

**Score 0 (Not acceptable)**:

- Users cannot safely predict what will happen when they perform basic actions.
- UI freezes/hangs or produces inconsistent results as a normal occurrence.

---

### 4.6 SHCS-OPS — Operations, Deployment & Upgrades

How painful it is to run and maintain the system.

**Score 3 (Industrial-grade)**:

- A minimal, production-viable deployment needs a small, well-defined set of
  services (e.g. app + DB + optional cache), with clear documentation and sane
  defaults.
- Upgrades are designed to be **automatic or one-command**, including schema
  migrations.
- The system surfaces actionable monitoring signals (logs, metrics) and fails
  loudly when invariants are broken.

**Score 2 (Solid)**:

- Deployment is well-documented and reasonably straightforward, even if several
  services are involved.
- Upgrades typically succeed but may require occasional manual intervention;
  operators can recover from failed upgrades without major data loss.

**Score 1 (Marginal)**:

- Deployment requires manual stitching of many components and undocumented
  assumptions.
- Upgrades are fragile and often require manual SQL/scripts; failures may leave
  the system inconsistent. Rollbacks are difficult.

**Score 0 (Not acceptable)**:

- Routine operations (deploy, upgrade) are unreliable, poorly documented, or known
  to break installations in unpredictable ways.

---

### 4.7 SHCS-ECO — Protocols, Clients & Ecosystem

How usable the system is in the real world, beyond the core engine.

**Score 3 (Industrial-grade)**:

- Multiple stable access paths exist: web UI, at least one sync client (desktop or
  mobile), and common protocols (e.g. WebDAV, S3-compatible, or others).
- An active ecosystem: plugins/integrations built on well-defined APIs, with
  documentation and examples for extending the system.
- Clients and integrations respect the core invariants and do not routinely
  compromise reliability.

**Score 2 (Solid)**:

- At least one mature client (desktop or mobile) and one standard protocol exist.
- Some third-party tooling or integrations are available; APIs exist and are used
  in a limited but meaningful way.

**Score 1 (Marginal)**:

- Only a web UI, or clients are experimental/unstable.
- Protocol support is narrow or unreliable; the ecosystem is minimal and
  extensions mostly live as "hacks".

**Score 0 (Not acceptable)**:

- No real-world access paths beyond a basic web UI; no integrations, no documented
  APIs.

---

## 5. SHCS Classes

Given the 0–3 scores for each axis, a system is assigned **exactly one** class.

Let `A_arch`, `A_perf`, `A_data`, `A_sec`, `A_ux`, `A_ops`, `A_eco` be the scores
(0–3), and let `M` be the arithmetic mean of all seven.

**How to assign a class:** evaluate the classes **top to bottom (A → B → C)** and
assign the **first** class whose conditions the system satisfies. If none match,
the class is **D**. This makes the classes mutually exclusive (first match wins)
and exhaustive (D is the catch-all) by construction.

### 5.1 Class A — Industrial-grade SHCS

The first class that matches, where:

- `M ≥ 2.5`, and
- `A_perf ≥ 3`, `A_data ≥ 3`, and `A_sec ≥ 3` (the three safety-critical axes are
  maxed), and
- no axis is below `2`.

Informally: a self-hosted cloud storage system suitable as a **primary storage
surface** for demanding use-cases — robust architecture, stable performance at
high load, strong data integrity, sound access control, predictable UX,
manageable operations.

### 5.2 Class B — Solid SHCS

Not Class A, and:

- `M ≥ 2.0`, and
- `A_data ≥ 2` and `A_sec ≥ 2` (safety floor: data and access control are at least
  solid), and
- no axis is `0`.

Informally: **safe to operate** for many scenarios. It may have known limitations
(perf on very large trees, weaker cryptographic integration, limited ecosystem),
but does not routinely endanger data, expose it, or exhaust operator sanity.

### 5.3 Class C — Legacy / Heavyweight SHCS

Not Class A or B, and:

- `M ≥ 1.5`, and
- `A_data ≥ 1` and `A_sec ≥ 1` (data integrity and access control are not in the
  "not acceptable" band).

Informally: heavy, legacy, or architecturally outdated systems — often with large
ecosystems and many features, but exhibiting poor performance scaling, fragile
upgrades, and/or weak data-integrity or security semantics. They can be acceptable
in some environments, but **do not represent modern expectations** for self-hosted
cloud storage.

### 5.4 Class D — Experimental / Toy

Any in-scope system that does not meet the conditions for A, B or C. This includes
(among others) systems with `M < 1.5`, or with a `0` on any axis, or with `A_data`
or `A_sec` of `0`.

Informally: prototypes, lab projects, or tools useful for niche or personal use,
but which **should not be relied upon as core storage**.

> Worked sanity-check of the rules. A system scoring `arch=0, perf=3, data=3,
> sec=2, ux=2, ops=2, eco=2` (M = 2.0) fails A (mean too low) and B (an axis is
> `0`), but matches C (`M ≥ 1.5`, data and sec ≥ 1) — so it is **C**, not
> unclassified. Under a naive "mean plus floors" scheme this system would fall
> through every class. First-match-plus-catch-all closes that gap.

---

## 6. Applying the rubric honestly

The rubric is only as good as the discipline applied when scoring. Whether you are
assessing your own system or someone else's:

1. **Publish the scores.** State all seven axis scores and the resulting class.
2. **Justify each axis** with 1–3 bullet points of concrete evidence, not
   adjectives.
3. **Round down when unsure.** If evidence is mixed or you are between two levels,
   take the lower one.
4. **Require evidence for top scores.** A `3` on SHCS-PERF, SHCS-DATA or SHCS-SEC
   should be backed by reproducible benchmarks, tests, or real-world deployments —
   not by assertion. Be especially conservative here: misclassifying data
   integrity, security, or scaling has real consequences (failed backups, partial
   restores, leaked or lost data).
5. **Disclose conflicts of interest.** If you are scoring your own system, say so.
   A self-assessment without reproducible evidence for the safety-critical axes
   should not claim Class A.

A self-assessment and a third-party assessment using these axes can be compared
directly; where they differ, the difference is itself informative.

---

## 7. What this rubric deliberately avoids

The rubric does **not**:

- prescribe specific technologies (e.g. "must be content-addressed", "must use
  Postgres");
- require specific protocols (WebDAV vs S3 vs NFS), as long as at least one
  standard, interoperable path exists;
- mandate open-source licensing (though it is oriented toward open-source
  ecosystems);
- claim to be neutral or authoritative. It is one informed, opinionated point of
  view (see Section 0).

The aim is an **implementation-agnostic way to talk about quality** — and to be
honest about whose opinion it represents.
