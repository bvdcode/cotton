# Cloud computation

Cloud and Remote computation use the same Text Embeddings Inference protocol: `GET info`, `POST tokenize`, and `POST embed`. Both validate the configured BGE-M3 model, CLS pooling, and 1024-dimensional vectors before using the service for search.

Cloud uses the Cotton Bridge base URL and the persistent instance credential. Telemetry must be enabled. Credentials are attached only to requests within the HTTPS Bridge base URL; a custom remote runner receives no Bridge credential. HTTP redirects are disabled for both clients.

Bridge authorizes the request and forwards the original protocol payload to the configured computation service. It records request metadata and input lengths without storing input text or vector values in the usage journal. Computation endpoints require credentials even during the temporary access period for older Bridge APIs.

The service advertises maximum inputs per request, tokens per input, and tokens per batch through `info`. Cotton uses these limits when splitting documents and forming embedding batches.

Background indexing reads up to 32 pending files per queue pass and combines their fragments into shared inference batches. Each batch respects both the advertised input count and the padded token budget (longest input multiplied by batch size). Extracted documents are read sequentially, without retaining all source texts together. Remaining fragments are sent immediately when the selected files are exhausted; there is no delay waiting for future files.

Results retain their file and fragment order. The selected files are committed together only after every inference response passes validation. Inference errors, cancellation, and database failures leave that group pending for retry without partial vector replacement. Indexing stops reading additional files when uploads start or global indexing is disabled, and finishes the files already read.

## Large documents

Tokenization requests contain at most 32,768 UTF-16 characters. Longer documents are split into windows and then into token-limited fragments without cutting UTF-8 characters or UTF-16 surrogate pairs. Automated checks cover one document with 1,048,576 characters, 65 documents with about 40,000 characters each, preservation of every fragment, batch limits, cancellation, and rollback.

This bounds request sizes, not total indexing memory. Extractors currently return each document as a complete string, and vectors for the selected file group remain in memory until database persistence. There is no configured extracted-text size limit. Indexing arbitrarily large documents can exhaust memory; multi-gigabyte document support is not established by these checks.
