export interface UploadServerParams {
  maxChunkSizeBytes: number;
  supportedHashAlgorithm: string;
}

export interface UploadFileToNodeOptions {
  // When false, we omit the chunk hash field on upload (server sees null).
  sendChunkHashForValidation?: boolean;

  // Parallel chunk uploads per file.
  concurrency?: number;
}

export interface UploadProgressSnapshot {
  // Current display progress. This includes confirmed chunks plus bytes sent by
  // active requests, and may decrease if a request is interrupted and retried.
  bytesUploaded: number;

  // Bytes acknowledged by successful chunk requests.
  bytesConfirmed: number;

  // Bytes currently sent by active chunk requests but not yet acknowledged.
  bytesInFlight: number;

  // Monotonic network-send counter used for speed estimation.
  bytesTransmitted: number;
}

export interface UploadFileToNodeCallbacks {
  onProgress?: (bytesUploaded: number, snapshot?: UploadProgressSnapshot) => void;
  onFinalizing?: () => void;
}
