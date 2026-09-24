# Cloud computation

Cloud and Remote computation use the same Text Embeddings Inference protocol: `GET info`, `POST tokenize`, and `POST embed`. Both validate the configured BGE-M3 model, CLS pooling, and 1024-dimensional vectors before using the service for search.

Cloud uses the Cotton Bridge base URL and the persistent instance credential. Telemetry must be enabled. Credentials are attached only to requests within the HTTPS Bridge base URL; a custom remote runner receives no Bridge credential. HTTP redirects are disabled for both clients.

Bridge authorizes the request and forwards the original protocol payload to the configured computation service. It records request metadata and input lengths without storing input text or vector values in the usage journal. Computation endpoints require credentials even during the temporary access period for older Bridge APIs.

The service advertises maximum inputs per request, tokens per input, and tokens per batch through `info`. Cotton uses these limits when splitting documents and forming embedding batches.

Background indexing reads up to 32 pending files per queue pass and combines their fragments into shared inference batches. Each batch respects both the advertised input count and the padded token budget (longest input multiplied by batch size). Extracted documents are read sequentially, without retaining all source texts together. Remaining fragments are sent immediately when the selected files are exhausted; there is no delay waiting for future files.

Results retain their file and fragment order. The selected files are committed together only after every inference response passes validation. Inference errors, cancellation, and database failures leave that group pending for retry without partial vector replacement. Indexing stops reading additional files when uploads start or global indexing is disabled, and finishes the files already read.

## Large documents

Tokenization requests contain at most 32,768 UTF-16 characters. Longer documents are split into windows and then into token-limited fragments without cutting UTF-8 characters or UTF-16 surrogate pairs. Automated checks cover one document with 1,048,576 characters, 65 documents with about 40,000 characters each, preservation of every fragment, batch limits, cancellation, and rollback.

Extracted text is limited per document to 4 MiB measured as UTF-8 bytes. Configure a positive byte count through `TextIndexing:MaxExtractedTextBytes` or the environment variable `TextIndexing__MaxExtractedTextBytes`. HTML and XHTML use a separate 64 MiB default, configured through `TextIndexing:MaxHtmlExtractedTextBytes` or `TextIndexing__MaxHtmlExtractedTextBytes`. Images and other non-text content do not consume the budget, so a large PDF with a small amount of text can still be indexed completely.

Files identified as CSV, TSV, JSON, XML, YAML, CSS, JavaScript, TypeScript, or Jupyter notebooks have an additional source-size limit of 1 MiB. Configure a positive byte count through `TextIndexing:MaxStructuredFileBytes` or `TextIndexing__MaxStructuredFileBytes`. Files exceeding it are skipped before reading their content or contacting the computation service. The manifest receives the current `TextIndexVersion` and a `file_too_large:` marker in `TextIndexError`. Classification uses the related files' content types; formats identified as plain text use the extracted-text budget instead. Previously indexed files retain their vectors and are not reindexed merely because a limit changes.

Generic HTML and XHTML parsing reads at most 16 MiB of source data and records the result as truncated when the source is larger. Scripted chat exports with an embedded `jsonData` conversation array are recognized separately: their active conversation branches are read directly from the JSON stream, without building an HTML document tree, and use the HTML extracted-text limit.

OpenDocument extraction rejects packages whose unpacked `content.xml` exceeds 16 MiB before building the XML document tree.

When text exceeds the budget, extraction returns the beginning without splitting a Unicode character. That prefix is indexed normally. The manifest receives the current `TextIndexVersion` and a `text_truncated:` marker with the configured byte limit in `TextIndexError`. Vectors and the marker are saved in the same transaction; the file is not retried automatically. This marker is stored in the database and logged, and is not currently shown in the file browser. A file whose text fits the budget has no truncation marker.

Text files are read in blocks. PDF extraction stops after the page containing the truncation point, and EPUB extraction stops before opening later chapters. Other extractors stop collecting text once the budget is exceeded. Automated checks cover Unicode boundaries, exact limits, early stopping, all supported extractor formats, large non-text EPUB entries, per-file budgets, and transactional persistence of the marker.

The text budget is not a process-memory limit. Document libraries may load a complete document, chapter, or page for parsing; archive and PDF source data may still need to be downloaded to a temporary file. The extracted prefix is held as a .NET string, and vectors for the selected file group remain in memory until database persistence. Multi-gigabyte document support is not established by these checks.
