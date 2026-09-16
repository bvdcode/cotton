import { z } from "zod";
import { isAxiosError } from "@shared/api/httpClient";

export const PGVECTOR_DOCUMENTATION = "https://github.com/pgvector/pgvector";
export const ENABLE_VECTOR_SQL = "CREATE EXTENSION IF NOT EXISTS vector;";
export const SUPPORTED_POSTGRES_VERSIONS = [13, 14, 15, 16, 17, 18];
export const PGVECTOR_RELEASE = "0.8.6";

const setupProblemSchema = z.object({
  code: z.enum(["pgvector_permission_denied", "pgvector_package_missing"]),
});

export const getVectorSetupFailure = (error: Error | null) => {
  if (!isAxiosError(error)) {
    return null;
  }
  const parsed = setupProblemSchema.safeParse(error.response?.data);
  if (!parsed.success) {
    return null;
  }
  return parsed.data.code;
};

export type DockerImageResult =
  | { kind: "empty" }
  | { kind: "invalid" }
  | { kind: "unsupported" }
  | { kind: "versionUnsupported" }
  | { kind: "mismatch" }
  | { kind: "chooseVariant" }
  | { kind: "ready"; image: string };

export const getPgvectorDockerImage = (
  currentImage: string,
  majorVersion: number,
  variant: string,
): DockerImageResult => {
  const value = currentImage.trim().replace(/^image:\s*/, "");
  if (!value) {
    return { kind: "empty" };
  }
  if (!SUPPORTED_POSTGRES_VERSIONS.includes(majorVersion)) {
    return { kind: "versionUnsupported" };
  }
  if (value.includes("alpine")) {
    return { kind: "unsupported" };
  }
  const match =
    /^(?:docker\.io\/)?(?:library\/)?postgres:(\d+)(?:\.\d+)*(?:-(bookworm|trixie))?$/.exec(
      value,
    );
  if (!match) {
    return { kind: "invalid" };
  }
  if (Number(match[1]) !== majorVersion) {
    return { kind: "mismatch" };
  }
  const base = match[2] ?? variant;
  if (base !== "bookworm" && base !== "trixie") {
    return { kind: "chooseVariant" };
  }
  return {
    kind: "ready",
    image: `pgvector/pgvector:pg${majorVersion}-${base}`,
  };
};
