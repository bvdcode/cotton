import type { AxiosInstance } from "axios";
import { getHttpOrThrow } from "./http";
import type { CottonResult, FileManifestDto, CreateFileRequest } from "../types/api";

/**
 * OOP-style API client responsible for file and layout related endpoints.
 * All calls acquire the current Axios instance lazily to ensure fresh auth headers.
 */
export class FilesApiClient {
  private readonly base = "/api/v1";

  /** Internal helper to build a URL relative to the API base */
  private url(path: string): string {
    return `${this.base}${path}`;
  }

  /** Optionally allow dependency injection for testing */
  private axios(): AxiosInstance {
    return getHttpOrThrow();
  }

  async chunkExists(hash: string): Promise<boolean> {
    const res = await this.axios().get(this.url(`/chunks/${encodeURIComponent(hash)}`), {
      validateStatus: () => true,
    });
    return res.status === 200;
  }

  async uploadChunk(blob: Blob, hash: string, fileName?: string): Promise<void> {
    const form = new FormData();
    form.append("file", blob, fileName ?? "chunk.bin");
    form.append("hash", hash);
    const res = await this.axios().post(this.url("/chunks"), form);
    if (res.status < 200 || res.status >= 300) {
      throw new Error(`Chunk upload failed: ${res.status}`);
    }
  }

  async createFileFromChunks(req: CreateFileRequest): Promise<FileManifestDto> {
    type Resp = CottonResult<FileManifestDto> | FileManifestDto;
    const { data } = await this.axios().post<Resp>(
      this.url("/files/from-chunks"),
      req,
    );
    if (this.isEnvelope<FileManifestDto>(data)) {
      if (!data.success || !data.data) {
        throw new Error(data.message || "Server returned empty response");
      }
      return data.data;
    }
    return data as FileManifestDto;
  }

  getDownloadUrl(fileId: string): string {
    return this.url(`/files/${encodeURIComponent(fileId)}/download`);
  }
  private isEnvelope<T>(val: unknown): val is CottonResult<T> {
    if (!val || typeof val !== "object") return false;
    const rec = val as Record<string, unknown>;
    return typeof rec["success"] === "boolean" && ("data" in rec || "message" in rec);
  }
}

export const filesApi = new FilesApiClient();
