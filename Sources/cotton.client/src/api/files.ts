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
  sha256: string; // server returns byte[]; we'll map to hex on server; assume string here for UI
}

export interface CreateFileRequest {
  chunkHashes: string[];
  name: string;
  contentType: string;
  sha256: string; // full file hash
  nodeId: string; // layout node id to attach the file to
}

export interface UploadChunkResponse {
  ok: boolean;
}

export async function uploadChunk(chunk: Blob, hash: string, filename?: string): Promise<UploadChunkResponse> {
  const formData = new FormData();
  formData.append("file", chunk, filename ?? "chunk.bin");
  formData.append("hash", hash);

  const res = await api.post(`${API_ENDPOINTS.chunk}`, formData, {
    // Let the browser set the multipart boundary automatically
    headers: { },
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

export async function createFileFromChunks(req: CreateFileRequest): Promise<FileManifestDto> {
  const res = await api.post<CottonResult<FileManifestDto>>(
    `${API_ENDPOINTS.files}`,
    req,
  );
  const envelope = res.data;
  if (!envelope.success || !envelope.data) throw new Error(envelope.message || "Unknown error");
  return envelope.data;
}
