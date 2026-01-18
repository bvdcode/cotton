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

export interface UploadFileToNodeCallbacks {
  onProgress?: (bytesUploaded: number) => void;
  onFinalizing?: () => void;
}
