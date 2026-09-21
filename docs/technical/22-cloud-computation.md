# Cloud computation

Cloud and Remote computation use the same Text Embeddings Inference protocol: `GET info`, `POST tokenize`, and `POST embed`. Both validate the configured BGE-M3 model, CLS pooling, and 1024-dimensional vectors before using the service for search.

Cloud uses the Cotton Bridge base URL and the persistent instance credential. Telemetry must be enabled. Credentials are attached only to requests within the HTTPS Bridge base URL; a custom remote runner receives no Bridge credential. HTTP redirects are disabled for both clients.

Bridge authorizes the request and forwards the original protocol payload to the configured computation service. It records request metadata and input lengths without storing input text or vector values in the usage journal. Computation endpoints require credentials even during the temporary access period for older Bridge APIs.

The service advertises maximum inputs per request, tokens per input, and tokens per batch through `info`. Cotton uses these limits when splitting documents and forming embedding batches.
