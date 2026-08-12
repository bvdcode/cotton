# 09. Upload and File Lifecycle

Cotton uses a chunk-first upload protocol. Binary data is accepted and verified before a logical file reference is created. This keeps retries cheap, enables deduplication, and prevents an incomplete upload from appearing as a finished file.

## Client contract

The normal browser upload follows this sequence:

1. Split the file according to the server-advertised maximum chunk size.
2. Compute SHA-256 for every chunk and for the complete file.
3. Probe which chunks are already available to the current user.
4. Upload only missing chunks.
5. Submit the ordered chunk hashes, target node, file name, and complete-file hash.
6. Handle a name conflict explicitly: replace, rename, skip, or apply the selected action to the remaining conflicts.

The relevant API families are:

- `GET /api/v1/chunks/{hash}/exists`
- `POST /api/v1/chunks/raw?hash={hash}`
- `POST /api/v1/files/from-chunks`
- file-content update operations under `/api/v1/files`

Client settings expose the supported hash algorithm and chunk-size limit. Clients must not assume a hard-coded server value.

## Chunk ingestion

A chunk upload is accepted only when all of these conditions hold:

- the authenticated caller is known;
- the body is non-empty and within the configured limit;
- the supplied hash is valid SHA-256;
- the bytes received by the server produce that exact hash;
- storage pressure permits new data;
- the object is not being reclaimed by garbage collection.

The server encrypts and stores the chunk before recording its database metadata and ownership proof. Concurrent uploads of the same content converge on the same content-addressed object. A uniqueness conflict is treated as successful deduplication only after the existing object has been verified as usable.

`ChunkOwnership` proves that a user uploaded or already possessed a chunk. It authorizes manifest assembly but does not keep an otherwise unreferenced chunk alive forever.

## Manifest assembly

Creating a file from chunks validates the complete ordered list before mutation:

- every hash has the correct format;
- every chunk exists in the database and storage;
- the caller has ownership proof for every chunk;
- the sum of stored plaintext lengths is coherent;
- the proposed whole-file hash has the supported format;
- the target layout and node belong to the caller;
- the requested name is valid and does not silently overwrite another item.

The manifest stores ordered chunk references and the logical file size. A `NodeFile` then attaches that content to the logical filesystem. Identical content may reuse a manifest, while each logical file keeps its own name, owner, location, metadata, and version lineage.

## Creating and replacing content

File creation and content replacement are quota mutations. The final quota check and database commit are serialized per user within the server process. This prevents parallel requests aimed at different folders from independently passing against the same stale usage value.

Replacing content is non-destructive when version history is enabled: the previous state is captured before the current file points at the new manifest. Optimistic concurrency information, when supplied by the client, prevents overwriting a newer edit.

After a successful commit the server emits the appropriate real-time event and schedules derived work such as complete-hash verification, metadata extraction, and preview generation. Failure of derived work does not roll back a valid uploaded file.

## Failure and retry behavior

- A hash mismatch is a client error; the object is not accepted under the claimed address.
- A name conflict is returned as a conflict, not converted into an implicit rename or replacement.
- A quota rejection occurs before the new logical reference is committed.
- Cancellation stops reads and storage work through the request cancellation token.
- A client may retry chunk probes and chunk uploads safely because content addressing makes the operations idempotent.
- Orphaned uploaded chunks are reclaimed later if no file, preview, avatar, backup, or other protected reference makes them live.

## Download path

Downloads resolve a logical file to its manifest and ordered chunks, then stream decrypted plaintext without assembling the complete file in memory. HTTP range requests are honored where the route supports them. Integrity and authorization checks happen before protected content is returned.

## Related sections

See [Content-Addressed Storage](04-content-addressed-storage.md), [Logical Filesystem](05-logical-filesystem.md), [Storage Pipeline and Backends](06-storage-pipeline.md), [Garbage Collection](10-garbage-collection.md), and [Sharing, Versions, Trash, Archives, and Quotas](11-sharing-versioning-trash-archives-quotas.md).
