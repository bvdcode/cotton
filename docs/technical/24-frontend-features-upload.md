# 24. Frontend Features and Upload Pipeline

The file browser coordinates navigation, selection, upload, conflict handling, move and copy actions, sharing, previews, versions, trash, and optional client-side encryption. Each workflow ultimately uses the same backend filesystem and storage contracts.

## File browser composition

The page is divided into focused concerns:

- data loading and active-folder resolution;
- selection and keyboard actions;
- file and folder mutations;
- upload-task coordination;
- move, copy, trash, and restore workflows;
- client-encryption policy and transformations;
- list or tile presentation;
- dialogs, menus, and transient overlays.

The route and server query identify the active folder. Breadcrumbs are derived from resolved topology and appear below the mobile action row so controls remain available without horizontal scrolling.

New-file and new-folder actions use distinct icons, labels, and semantics. Icon-only mobile variants retain accessible labels and consistent placement.

## Upload preparation

Dropping files or folders is a staged operation:

1. scan supported browser entries;
2. map relative paths;
3. create required folders;
4. detect name conflicts;
5. apply explicit per-item or apply-to-all decisions;
6. enqueue accepted files.

The UI reports preparation progress independently from binary upload progress. Cancellation during preparation prevents remaining tasks from being enqueued.

## Conflict handling

A conflicting file presents clear actions such as replace, upload with a generated unique name, skip, skip all, or cancel. Folder conflicts are merged or renamed only according to the explicit workflow; they are not overwritten as files.

The dialog layout keeps the conflicting name readable and gives each action enough width on desktop and mobile. Leaving the dialog without a decision is treated as cancellation, not as replacement.

Apply-to-all choices affect only compatible remaining conflicts. A skip result advances to the next item and does not fail the entire batch.

## Upload execution

Each accepted file becomes a task with cancellation and progress:

1. read and optionally encrypt content in the browser;
2. split it using server-advertised chunk size;
3. hash chunks and the complete upload representation;
4. probe chunk ownership/existence;
5. upload missing chunks with bounded concurrency;
6. create or replace the logical file from the ordered hashes;
7. reconcile task and file-list state.

The task manager queues normal workload instead of relying on server `429` responses for flow control. Failures are isolated per file, and the task panel distinguishes failed, cancelled, skipped, and completed work.

## Client-side encryption behavior

When effective folder policy requires client encryption, encryption happens before hashing and upload. The server stores and streams the encrypted representation and cannot decrypt the user's original content or display metadata.

Moving plaintext content into an encrypted-policy folder may schedule explicit encryption tasks. Moving encrypted content out does not silently decrypt it. Recursive scans are bounded and report when they could not inspect the complete subtree.

The vault must be unlocked for transformations that require the client key. A failed transformation does not leave the UI claiming the file was converted successfully.

## Downloads and previews

The client chooses direct browser playback, HLS, preview, lightbox, editor, or download according to content metadata and server capability. URLs are obtained through the corresponding API rather than reconstructed from entity internals.

Client-encrypted content is decrypted in the browser for supported local workflows. Server-generated previews and server-side media transcoding are unavailable when the server cannot read the original plaintext.

## Reconciliation

Optimistic updates are limited to reversible, well-identified changes. Mutations invalidate or refetch the authoritative query when the server may have changed names, versions, metadata, quota, or subtree structure.

SignalR reduces visible delay but is not required for correctness. Reloading the active folder must always reconstruct the same final state.

## Related sections

See [Frontend Architecture](23-frontend-architecture.md), [Upload and File Lifecycle](09-upload-file-lifecycle.md), [Sharing, Versions, Trash, Archives, and Quotas](11-sharing-versioning-trash-archives-quotas.md), and [Previews and Media Processing](18-previews-media.md).
