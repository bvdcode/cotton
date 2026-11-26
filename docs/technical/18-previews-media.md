# 18. Previews and Media Processing

Previews and media playback are derived views of stored file content. Failure to generate or transcode a derived representation never changes the validity of the original file.

## Preview generation

The recurring preview job selects eligible manifests that do not have a current preview result. It skips empty files and active uploads, opens the file through the normal storage pipeline, chooses a generator by content type, and stores the resulting WebP bytes as content-addressed data. A maintenance job clears previously stored preview data for the empty-content manifest using its indexed content hash.

Image, document, vector, sticker, 3D, audio and video preview generators use managed .NET libraries. Mediana also reads audio and video metadata.

| Source | Processing path |
| --- | --- |
| Raster images | ImageSharp decoding, resizing, and WebP encoding. |
| HEIC/HEIF | Mediana.Images.Heif decoding, then ImageSharp. |
| Camera RAW | MetadataExtractor locates an embedded JPEG, then ImageSharp decodes it. |
| PDF | Mediana.Documents.Pdf renders the first page, then ImageSharp encodes it. |
| Word (`.docx`) | Mediana.Documents.Word lays out the document and renders the first page directly, then ImageSharp encodes it. |
| PowerPoint (`.pptx`) | Mediana.Documents.Presentation renders the first slide directly, then ImageSharp encodes it. |
| Excel (`.xls`, `.xlsx`) | Mediana.Documents.Xls/Xlsx reads cells; Mediana.Drawing draws a worksheet grid; ImageSharp resizes and encodes it. |
| SVG/SVGZ | Mediana.Images.Svg renders to pixels, then ImageSharp encodes them. |
| Telegram TGS | Mediana.Images.Tgs renders a static poster, then ImageSharp encodes it. |
| STL, OBJ, 3MF | Mediana.Models.Mesh renders geometry on the CPU; 3MF can use an embedded thumbnail. |
| Plain text | Mediana.Drawing draws text with its bundled fonts; ImageSharp encodes it. |
| Audio and video | Mediana reads artwork, extracts video frames and decodes audio for waveforms. ImageSharp encodes preview images as WebP. |

PDF previews render the first page with managed Mediana.Documents.Pdf and encode it through ImageSharp. PowerPoint previews render the selected first slide directly through Mediana.Documents.Presentation; other slides are not rendered, and no intermediate PDF is produced. Word previews render directly through Mediana.Documents.Word. Invalid input and features explicitly rejected by the readers fail preview generation. PDF uses generator version `3`, Word uses version `4`, and PowerPoint uses version `5`. PDF text extraction uses Mediana.Documents.Pdf; neither path requires PDFium or Docnet.

Office thumbnails are derived summaries, not a full Office layout engine. Word and PowerPoint use Mediana font substitution and embedded font resources; Cotton supplies no custom font resolvers. Word documents without a page size use A4; section page sizes and fractional measurements are preserved. PowerPoint reads the selected slide and its referenced layout, master, theme and images. PowerPoint rendering rejects required characters that its font resources cannot resolve. Spreadsheet thumbnails show up to six columns and fourteen rows from the first worksheet, preserving missing row and column coordinates; they do not reproduce workbook formatting, charts, or pagination.

The PowerPoint reader currently requires the complete compressed presentation in memory even when only the first slide is rendered. Source size therefore contributes directly to preview memory requirements.

For JPEG images embedded in PowerPoint slides, JFIF chroma positioning takes precedence when JFIF and EXIF disagree. Other image validation remains enabled.

Text, worksheet grids and audio waveforms use Mediana.Drawing antialiasing. Text renders directly at the requested size; worksheet grids use a 640 × 480 canvas and are only reduced for smaller previews. Text and worksheet glyphs are resolved by Mediana using its bundled fonts, without scanning installed fonts. Text and spreadsheet generators use version `2`; audio uses version `5`. ImageSharp.Drawing, SixLabors.Fonts, EasyExtensions.Drawing and EasyExtensions.Fonts are no longer required.

Searchable text from PDF, DOCX, PPTX and XLSX is extracted with Mediana without rendering or loading fonts. DOCX extraction includes headers and footers; PPTX extraction follows the declared slide order and includes speaker notes; XLSX extraction includes worksheet names and cached cell values. PPTX text is read from package parts without loading embedded media, and does not require slide dimensions. Invalid or unsupported document content is recorded as a file indexing error without stopping other documents in the batch.

Word previews render supported emoji with the embedded Microsoft Fluent Flat artwork through Mediana.Drawing.Emoji.Fluent. Rendering uses no installed emoji font or network access. Font substitution and missing-character rendering follow Mediana’s built-in policy.

SVG and SVGZ previews use managed Mediana.Images.Svg. TGS posters use Mediana.Images.Tgs, omit Puppet deformation effects and use stored property values without evaluating expressions. Both encode WebP through ImageSharp, keep a transparent background and reject other unsupported features instead of rendering partial content. SVG uses generator version `5`; TGS and HEIC use version `6`, and the three mesh formats use version `12`. SVG input and expanded SVGZ are limited to 16 MiB; both vector renderers have a 20-second cancellation timeout. Neither path requires SkiaSharp, Skottie or HarfBuzzSharp.

Camera RAW previews find embedded JPEG images in CR2, CR3, NEF, NRW, ARW, DNG, RAF, ORF, RW2, PEF, and SRW files. The small preview uses the smallest embedded image with enough pixels; the large preview uses the largest. The source file's orientation is applied before encoding both results as WebP. The RAW sensor data is not developed; files without a usable embedded JPEG cannot produce a preview. Source size, JPEG candidates, and decoded pixel count are bounded.

Files named as HEIC or HEIF are checked for JPEG, PNG, GIF, BMP, WebP and TIFF signatures in memory. A matching signature uses the ordinary image generator; other inputs use the HEIF decoder.

Video previews use embedded cover art when available. Otherwise, clips up to one second use the first frame; longer clips use a frame from the middle.

Telegram `.tgs` stickers are gzip-compressed Lottie animations. The preview generator renders a frame from the middle of the animation as a static WebP poster. Expressions are never executed; properties use their stored values and keyframes. It limits the expanded JSON to 8 MiB. A public file-share page displays the poster when it exists; animated playback is not provided.

Preview metadata is written only after the derived object exists. Each new successful result records the stable identifier and version of the generator that produced it. Only changes to that generator's version make the result eligible for automatic regeneration. Existing cached previews without a recorded generator remain available and are excluded from version-based regeneration; their generator is not inferred from current filenames.

Failed attempts use a fingerprint of the registered generators to retry after the available processing capabilities change. A failed refresh preserves the previous cached image and does not retry on every job run. Re-running generation is idempotent because identical output has the same storage hash.

## Failure classification

An unsupported or corrupt individual file records a preview-generation failure and the job continues. Process cancellation, storage unavailability, and unexpected infrastructure errors remain real failures and are logged accordingly.

Metadata extraction distinguishes unsupported media and timeouts from request cancellation and storage errors.

Temporary files used by generators that require filesystem paths are removed in `finally`. RAW generation creates a bounded temporary copy to scan and decode embedded images. Other plaintext media is streamed from encrypted storage rather than materialized as one complete temp file.

## Content metadata

Extracted content metadata is stored in `file_manifests.metadata` as a flat string dictionary. `metadataExtractorVersion` records the extraction version, currently `"2"`. The extraction job runs every 15 minutes and processes up to 500 eligible manifests whose version differs from the current version. Legacy processed flags and image dimensions do not prevent reprocessing. Reprocessing replaces the extractor-owned `contentMetadata.*`, `image.*`, and `media.*` keys while retaining custom metadata outside those namespaces. This also removes oversized fields from earlier extraction versions as files are processed. The extraction endpoint applies the same version check.

Image extraction retains a selected set of useful fields: dimensions, format, camera and lens make/model, orientation, capture dates, exposure, aperture, ISO, exposure bias, flash, focal length, white balance, and GPS coordinates/altitude. HEIC, HEIF, AVIF and supported camera RAW files use the same extraction pipeline. Keys preserve the existing directory/tag shape, for example `image.Exif IFD0.0.tags.Make.271`; duplicate tags from repeated directories are stored once. GPS also includes decimal `image.GPS.0.latitude` and `image.GPS.0.longitude` values. XMP packets, ICC profiles, manufacturer data blocks, embedded thumbnails, copyright tags, and parser diagnostics are excluded.

Selected tag values preserve numeric precision and rational numerators and denominators. Text values longer than 1,024 characters are omitted. Strings containing a zero byte use JSON string escaping with `.encoding = "json"`, because PostgreSQL text cannot store zero bytes. Periods and tildes within source key segments are escaped as `~1` and `~0` respectively; zero bytes use `~2`. Image input is copied asynchronously to a temporary file that is deleted when extraction ends; the synchronous metadata parser reads that local copy. If formatting a selected tag's description fails, its value and the other tags remain available and the failure is logged.

Audio and video extraction retains available fields from Mediana container and stream metadata: duration, container and codecs, dimensions, frame rate, rotation, pixel/color information, audio sample rate/channels, bitrate, language, dates, location, and selected title/artist/album/track/disc/genre tags. Examples include `media.format.tags.location`, `media.streams.0.codec_name` and `media.streams.1.tags.language`. Programs, chapters, arbitrary tags, and unrelated side data are excluded. The summary keys used by audio playback remain available. Selected metadata tags are bounded to 4 MiB in total; exceeding the limit fails extraction rather than storing a truncated result. Unavailable probes and infrastructure errors do not record the current extraction version.

Supported metadata depends on the source format and the reader's understanding of it. Extraction keeps the selected fields that are present; it does not preserve the complete source metadata tree.

## Serving previews

Preview URLs use encrypted references to immutable content hashes. The preview endpoint verifies the owning row and storage reference before opening the blob.

Serving is bounded by a process-wide semaphore. Requests wait for available capacity instead of receiving `429` during normal user activity. This protects storage and decryption throughput while preserving the expectation that a valid preview eventually loads.

Immutable preview responses use strong validators and long-lived caching. A matching entity tag returns `304`; a cache miss streams the stored WebP object through the normal decryption and decompression pipeline.

Preview and avatar hashes are durable liveness references for garbage collection.

## Seekable media source

Mediana reads container metadata and media packets directly from the seekable concatenated stream backed by encrypted storage. The stream is seekable when the manifest supplies the total size and individual chunk lengths.

## HLS playback

Files natively supported by the browser are streamed directly. Eligible non-native video can be exposed as on-demand HLS:

1. Resolve the download token and source file using the same public-share protection as direct download.
2. Verify token, file graph, node state, and media eligibility.
3. Plan frame-aligned segments with Mediana under the probe concurrency gate.
4. Build a VOD playlist for a supported rendition.
5. Encode requested MPEG-TS segments with Mediana under the transcode concurrency gate. The Source rendition keeps the original resolution.
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
