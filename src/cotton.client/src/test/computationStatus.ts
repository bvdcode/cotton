import type { ComputationStatus } from "../shared/api/computation";

export const readyComputationStatus: ComputationStatus = {
  isReady: true,
  dimensions: 1024,
  error: null,
  info: {
    modelId: "BAAI/bge-m3",
    modelRevision: null,
    isEmbeddingModel: true,
    pooling: "cls",
    maxInputTokens: 8192,
    maxBatchTokens: 16384,
    maxBatchInputs: 32,
    maxConcurrentRequests: 512,
  },
};
