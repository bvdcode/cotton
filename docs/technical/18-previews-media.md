# 18. Previews and Media Processing

Previews and media playback are derived views of stored file content. Failure to generate or transcode a derived representation never changes the validity of the original file.

## Preview generation

The recurring preview job selects eligible manifests that do not have a current preview result. It skips empty files and active uploads, opens the file through the normal storage pipeline, chooses a generator by content type, and stores the resulting WebP bytes as content-addressed data. A maintenance job clears previously stored preview data for the empty-content manifest using its indexed content hash.

Image, document, vector, sticker, and 3D preview generators use managed .NET libraries. Audio and video generators use FFmpeg/ffprobe. The official container includes the required media binaries; other deployments must provide them.

| Source | Processing path |
| --- | --- |
| Raster images | ImageSharp decoding, resizing, and WebP encoding. |
| HEIC/HEIF | Heic.NET decoding, then ImageSharp. |
| Camera RAW | MetadataExtractor locates an embedded JPEG, then ImageSharp decodes it. |
| PDF | Render.NET.Pdf renders the first page, then ImageSharp encodes it. |
| Word (`.docx`) and PowerPoint (`.pptx`) | OoxPDF converts to PDF, then the PDF generator renders the first page or slide. |
| Excel (`.xls`, `.xlsx`) | ExcelDataReader reads cells; ImageSharp draws a worksheet grid. |
| SVG/SVGZ | Render.NET.Svg renders to pixels, then ImageSharp encodes them. |
| Telegram TGS | Render.NET.Tgs renders a static poster, then ImageSharp encodes it. |
| STL, OBJ, 3MF | Render.NET.Mesh renders geometry on the CPU; 3MF can use an embedded thumbnail. |
| Plain text | ImageSharp draws text with bundled fonts. |
| Audio and video | FFmpeg/ffprobe extracts artwork, frames, and media information. |

PDF previews render the first page with managed Render.NET.Pdf and encode it through ImageSharp. Word and PowerPoint previews use the same renderer after conversion to PDF. Unsupported page features fail preview generation rather than producing a partial page. PDF and Office generator versions are `1`. PDF text extraction uses PdfPig; neither path requires PDFium or Docnet.

Office thumbnails are derived summaries, not a full Office layout engine. Word and PowerPoint conversion uses the bundled Tuffy font. The current OoxPDF converter can omit text in standard PowerPoint placeholders. Spreadsheet thumbnails show up to six columns and fourteen rows from the first worksheet; they do not reproduce workbook formatting, charts, or pagination.

SVG and SVGZ previews use managed Render.NET.Svg. TGS posters use Render.NET.Tgs. Both encode WebP through ImageSharp, keep a transparent background and reject unsupported features instead of rendering partial content. Their generator versions are `2`. SVG input and expanded SVGZ are limited to 16 MiB; both renderers have a 20-second cancellation timeout. Neither path requires SkiaSharp, Skottie or HarfBuzzSharp.

Camera RAW previews find embedded JPEG images in CR2, CR3, NEF, NRW, ARW, DNG, RAF, ORF, RW2, PEF, and SRW files. The small preview uses the smallest embedded image with enough pixels; the large preview uses the largest. The source file's orientation is applied before encoding both results as WebP. The RAW sensor data is not developed; files without a usable embedded JPEG cannot produce a preview. Source size, JPEG candidates, and decoded pixel count are bounded.

Files named as HEIC or HEIF are checked for JPEG, PNG, GIF, BMP, WebP and TIFF signatures in memory. A matching signature uses the ordinary image generator; other inputs use the HEIF decoder.

Video previews use embedded cover art when available. Otherwise, clips up to one second use the first frame; longer clips use a frame from the middle.

Telegram `.tgs` stickers are gzip-compressed Lottie animations. The preview generator renders a frame from the middle of the animation as a static WebP poster. It limits the expanded JSON to 8 MiB. A public file-share page displays the poster when it exists; animated playback is not provided.

Preview metadata is written only after the derived object exists. Each new successful result records the stable identifier and version of the generator that produced it. Only changes to that generator's version make the result eligible for automatic regeneration. Existing cached previews without a recorded generator remain available and are excluded from version-based regeneration; their generator is not inferred from current filenames.

Failed attempts use a fingerprint of the registered generators to retry after the available processing capabilities change. A failed refresh preserves the previous cached image and does not retry on every job run. Re-running generation is idempotent because identical output has the same storage hash.

## Failure classification

An unsupported or corrupt individual file records a preview-generation failure and the job continues. Process cancellation, storage unavailability, and unexpected infrastructure errors remain real failures and are logged accordingly.

`ffmpeg` and `ffprobe` exit status, timeout, cancellation, and startup errors are handled separately. A generic catch must not convert an infrastructure outage into a permanent "unsupported file" result.

Temporary files used by generators that require filesystem paths are removed in `finally`. RAW generation creates a bounded temporary copy to scan and decode embedded images. Other plaintext media is streamed from encrypted storage rather than materialized as one complete temp file.

## Content metadata

Extracted content metadata is stored in `file_manifests.metadata` as a flat string dictionary. `metadataExtractorVersion` records the extraction version, currently `"2"`. The extraction job runs every 15 minutes and processes up to 500 eligible manifests whose version differs from the current version. Legacy processed flags and image dimensions do not prevent reprocessing. Reprocessing replaces the extractor-owned `contentMetadata.*`, `image.*`, and `media.*` keys while retaining custom metadata outside those namespaces. This also removes oversized fields from earlier extraction versions as files are processed. The extraction endpoint applies the same version check.

Image extraction retains a selected set of useful fields: dimensions, format, camera and lens make/model, orientation, capture dates, exposure, aperture, ISO, exposure bias, flash, focal length, white balance, and GPS coordinates/altitude. HEIC, HEIF, AVIF and supported camera RAW files use the same extraction pipeline. Keys preserve the existing directory/tag shape, for example `image.Exif IFD0.0.tags.Make.271`; duplicate tags from repeated directories are stored once. GPS also includes decimal `image.GPS.0.latitude` and `image.GPS.0.longitude` values. XMP packets, ICC profiles, manufacturer data blocks, embedded thumbnails, copyright tags, and parser diagnostics are excluded.

Selected tag values preserve numeric precision and rational numerators and denominators. Text values longer than 1,024 characters are omitted. Strings containing a zero byte use JSON string escaping with `.encoding = "json"`, because PostgreSQL text cannot store zero bytes. Periods and tildes within source key segments are escaped as `~1` and `~0` respectively; zero bytes use `~2`. Image input is copied asynchronously to a temporary file that is deleted when extraction ends; the synchronous metadata parser reads that local copy. If formatting a selected tag's description fails, its value and the other tags remain available and the failure is logged.

Audio and video extraction requests an explicit ffprobe field list: duration, container and codecs, dimensions, frame rate, rotation, pixel/color information, audio sample rate/channels, bitrate, language, dates, location, and selected title/artist/album/track/disc/genre tags. Examples include `media.format.tags.location`, `media.streams.0.codec_name` and `media.streams.1.tags.language`. Programs, chapters, arbitrary tags, and unrelated side data are excluded. The summary keys used by audio playback remain available. Probe output is bounded to 4 MiB; exceeding the limit fails extraction rather than storing a truncated result. Unavailable probes and infrastructure errors do not record the current extraction version.

Supported metadata depends on the source format and the reader's understanding of it. Extraction keeps the selected fields that are present; it does not preserve the complete source metadata tree.

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

Administrators may provide explicit executable paths through `COTTON_FFMPEG_PATH` and `COTTON_FFPROBE_PATH`. Otherwise the runtime resolves `ffmpeg` and `ffprobe` on `PATH`. The official container installs them as system packages; Cotton does not download binaries at runtime. An explicit invalid path is an error; it is not silently replaced with a different binary.

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
