# 18. Previews and Media Processing

Previews and media playback are derived views of stored file content. Failure to generate or transcode a derived representation never changes the validity of the original file.

## Preview generation

The recurring preview job selects eligible manifests that do not have a current preview result. It skips active uploads, opens the file through the normal storage pipeline, chooses a generator by content type, and stores the resulting WebP bytes as content-addressed data.

Supported generator families include images, HEIC, documents, text, audio, video, and selected 3D formats. Generator availability depends on the runtime libraries and external binaries present in the deployment.

Preview metadata is written only after the derived object exists. Re-running generation is idempotent because identical output has the same storage hash. A generator-version change may make an older result eligible for regeneration without changing the source file.

## Failure classification

An unsupported or corrupt individual file records a preview-generation failure and the job continues. Process cancellation, storage unavailability, and unexpected infrastructure errors remain real failures and are logged accordingly.

`ffmpeg` and `ffprobe` exit status, timeout, cancellation, and startup errors are handled separately. A generic catch must not convert an infrastructure outage into a permanent "unsupported file" result.

Temporary files used by generators that require filesystem paths are removed in `finally`. Original plaintext media is otherwise streamed from encrypted storage rather than materialized as one complete temp file.

## Serving previews

Preview URLs use encrypted references to immutable content hashes. The preview endpoint verifies the owning row and storage reference before opening the blob.

Serving is bounded by a process-wide semaphore. Requests wait for available capacity instead of receiving `429` during normal user activity. This protects storage and decryption throughput while preserving the expectation that a valid preview eventually loads.

Immutable preview responses use strong validators and long-lived caching. A matching entity tag returns `304`; a cache miss streams the stored WebP object through the normal decryption and decompression pipeline.

Preview and avatar hashes are durable liveness references for garbage collection.

## Seekable media source

Media tools often seek to metadata near the end of a file and may issue concurrent range reads. Cotton exposes the chunked source through a loopback range server backed by the seekable concatenated stream.

The source stream is seekable only when the manifest supplies total size and individual chunk lengths. Access to the shared underlying position is serialized for each seek-and-read operation, while writing the response to the media process occurs outside that critical section. This permits concurrent range connections without corrupting source position or holding a lock during slow consumer writes.

Shutdown drains active handlers for a bounded time, cancels the accept loop, and disposes the listener.

## External binaries

Administrators may provide explicit `ffmpeg` and `ffprobe` executable paths through configuration. Otherwise the runtime resolves or provisions supported binaries according to the packaged media configuration. An explicit invalid path is an error; it is not silently replaced with a different binary.

Every external process has a bounded lifetime or request cancellation, captures diagnostic output, and kills the process tree on timeout or cancellation.

## HLS playback

Files natively supported by the browser are streamed directly. Eligible non-native video can be exposed as on-demand HLS:

1. Resolve the download token and source file using the same public-share protection as direct download.
2. Verify token, file graph, node state, and media eligibility.
3. Probe duration and codecs under the probe concurrency gate.
4. Build a VOD playlist for a supported rendition.
5. Transcode requested segments under the transcode concurrency gate.
6. Cache completed segments in a size-bounded memory cache.

HLS does not require a longer or separate secret. It uses the same download-token authority as the file it represents. Compact-token lookup protection applies before resolution on playlist, probe, and segment routes.

Segments are generated lazily and are not persisted as durable storage objects. Restarting the process clears probe and segment caches but does not affect the source file.

## Performance and isolation

- Preview serving, probing, and transcoding have independent concurrency limits.
- Background generation yields to active uploads and works in bounded batches.
- HEIC and other stream copies use asynchronous I/O.
- S3-backed hashing and media reads do not synchronously block worker threads.
- One failed item does not stop unrelated preview work.
- Cache limits bound process memory used by HLS segments.

## Related sections

See [Storage Pipeline and Backends](06-storage-pipeline.md), [Upload and File Lifecycle](09-upload-file-lifecycle.md), [Garbage Collection](10-garbage-collection.md), and [Background Jobs](15-background-jobs.md).
