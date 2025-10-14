import { API_BASE_URL, API_ENDPOINTS, buildDownloadUrl } from "../config.ts";

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
  sha256: string; // server returns byte[]; we'll map to hex on server; assume string here for UI
}

export interface CreateFileRequest {
  chunkHashes: string[];
  name: string;
  folder: string;
  contentType: string;
  sha256: string; // full file hash
}

export interface UploadChunkResponse {
  ok: boolean;
}

export async function uploadChunk(chunk: Blob, hash: string, filename?: string): Promise<UploadChunkResponse> {
  const formData = new FormData();
  formData.append("file", chunk, filename ?? "chunk.bin");
  formData.append("hash", hash);

  const res = await fetch(`${API_BASE_URL}${API_ENDPOINTS.chunk}`, {
    method: "POST",
    body: formData,
  });
  if (!res.ok) {
    const text = await res.text().catch(() => "");
    throw new Error(`Chunk upload failed: ${res.status} ${text}`);
  }
  // If server returns something meaningful, parse it; for now just ok
  return { ok: true };
}

export async function listFiles(): Promise<FileManifestDto[]> {
  const res = await fetch(`${API_BASE_URL}${API_ENDPOINTS.files}`);
  if (!res.ok) throw new Error(`Files fetch failed: ${res.status}`);
  const envelope = (await res.json()) as CottonResult<FileManifestDto[]>;
  if (!envelope.success) throw new Error(envelope.message);
  return envelope.data ?? [];
}

export function getDownloadUrl(fileManifestId: string): string {
  return buildDownloadUrl(fileManifestId);
}

export async function createFileFromChunks(req: CreateFileRequest): Promise<FileManifestDto> {
  const res = await fetch(`${API_BASE_URL}${API_ENDPOINTS.files}`, {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify(req),
  });
  if (!res.ok) throw new Error(`Create file failed: ${res.status}`);
  const envelope = (await res.json()) as CottonResult<FileManifestDto>;
  if (!envelope.success || !envelope.data) throw new Error(envelope.message || "Unknown error");
  return envelope.data;
}
