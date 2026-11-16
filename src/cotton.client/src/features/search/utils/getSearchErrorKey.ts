import { getComputationError } from "@shared/api/computation";

export function getSearchErrorKey<T>(error: T, deep: boolean): string {
  if (!deep) {
    return "error";
  }

  const code = getComputationError(error instanceof Error ? error : null);
  switch (code) {
    case "NotConfigured":
    case "UnsupportedMode":
    case "InvalidUrl":
      return "smartSearch.notConfigured";
    case "Unreachable":
    case "Timeout":
    case "InvalidResponse":
    case "NotEmbeddingModel":
    case "IncompatibleModel":
    case "InvalidDimensions":
    case "InvalidVectors":
    case "InvalidInput":
    case "AccessDenied":
    case null:
      return "smartSearch.error";
  }
}
