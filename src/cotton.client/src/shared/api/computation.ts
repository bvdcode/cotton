import { z } from "zod";
import { isAxiosError } from "./httpClient";

export const computationErrorSchema = z.enum([
  "NotConfigured",
  "UnsupportedMode",
  "InvalidUrl",
  "Unreachable",
  "Timeout",
  "InvalidResponse",
  "NotEmbeddingModel",
  "IncompatibleModel",
  "InvalidDimensions",
  "InvalidVectors",
  "InvalidInput",
]);

export type ComputationError = z.infer<typeof computationErrorSchema>;

export const computationStatusSchema = z.object({
  isReady: z.boolean(),
  dimensions: z.number().int().positive().nullable(),
  error: computationErrorSchema.nullable(),
  info: z
    .object({
      modelId: z.string(),
      modelRevision: z.string().nullable(),
      isEmbeddingModel: z.boolean(),
      pooling: z.string().nullable(),
      maxInputTokens: z.number().int().positive(),
      maxBatchTokens: z.number().int().positive(),
      maxBatchInputs: z.number().int().positive(),
      maxConcurrentRequests: z.number().int().positive(),
    })
    .nullable(),
});

export type ComputationStatus = z.infer<typeof computationStatusSchema>;

const problemSchema = z.object({ code: computationErrorSchema });

export const getComputationError = (
  error: Error | null,
): ComputationError | null => {
  if (!isAxiosError(error)) {
    return null;
  }
  const parsed = problemSchema.safeParse(error.response?.data);
  return parsed.success ? parsed.data.code : null;
};
