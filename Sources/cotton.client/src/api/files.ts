import { API_BASE_URL, API_ENDPOINTS } from "../config.ts";

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
