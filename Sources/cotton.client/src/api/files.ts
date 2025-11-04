import { API_ENDPOINTS, buildDownloadUrl } from "../config.ts";
import api from "./http.ts";

// CottonResult envelope
interface CottonResult<T> {
  success: boolean;
  message: string;
  data: T | null;
}

export interface FileManifestDto {
  id: string; // Guid
  ownerId?: string | null;
  name: string;
  folder: string;
  contentType: string;
  sizeBytes: number;
  hash: string; // server returns byte[]; we'll map to hex on server; assume string here for UI
}

export interface CreateFileRequest {
  chunkHashes: string[];
  name: string;
  contentType: string;
  hash: string; // full file hash
  nodeId: string; // layout node id to attach the file to
}

export interface UploadChunkResponse {
  ok: boolean;
}

/**
 * Check whether a chunk with the given hash already exists on the server.
 * Returns true if server responds 200, false if 404. Any other status will also
 * be treated as "not found" to allow best-effort uploads instead of failing early.
 */
export async function chunkExists(hash: string): Promise<boolean> {
  const res = await api.get(`${API_ENDPOINTS.chunk}/${encodeURIComponent(hash)}`, {
    // We want to inspect status codes directly
    validateStatus: () => true,
  });
  return res.status === 200;
}

export async function uploadChunk(
  chunk: Blob,
  hash: string,
  filename?: string,
): Promise<UploadChunkResponse> {
  const formData = new FormData();
  formData.append("file", chunk, filename ?? "chunk.bin");
  formData.append("hash", hash);

  const res = await api.post(`${API_ENDPOINTS.chunk}`, formData, {
    // Let the browser set the multipart boundary automatically
    headers: {},
  });
  if (res.status < 200 || res.status >= 300) {
    throw new Error(`Chunk upload failed: ${res.status}`);
  }
  return { ok: true };
}

export async function listFiles(): Promise<FileManifestDto[]> {
  const res = await api.get<CottonResult<FileManifestDto[]>>(
    `${API_ENDPOINTS.files}`,
  );
  const envelope = res.data;
  if (!envelope.success) throw new Error(envelope.message);
  return envelope.data ?? [];
}

export function getDownloadUrl(fileManifestId: string): string {
  return buildDownloadUrl(fileManifestId);
}

type CreateFileResponse = CottonResult<FileManifestDto> | FileManifestDto;

export async function createFileFromChunks(
  req: CreateFileRequest,
): Promise<FileManifestDto> {
  const res = await api.post<CreateFileResponse>(
    `${API_ENDPOINTS.files}/from-chunks`,
    req,
  );
  const data = res.data;
  if (isEnvelope<FileManifestDto>(data)) {
    if (!data.success || !data.data) {
      throw new Error(data.message || "Server returned empty response");
    }
    return data.data;
  }
  return data as FileManifestDto;
}

function isEnvelope<T>(val: unknown): val is CottonResult<T> {
  if (!val || typeof val !== "object") return false;
  const rec = val as Record<string, unknown>;
  return (
    typeof rec["success"] === "boolean" && ("data" in rec || "message" in rec)
  );
}
