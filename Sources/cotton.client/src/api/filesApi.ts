import type { AxiosInstance } from "axios";
import { getHttpOrThrow } from "./http";
import type {
  ChildrenResponse,
  CreateFileFromChunksRequest,
  CreateFolderRequest,
  ChunkExistsResponse,
  FileItem,
  NodeItem,
} from "../types/files";

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
    const { data } = await this.axios().get<ChunkExistsResponse>(
      this.url(`/files/chunks/${encodeURIComponent(hash)}`),
    );
    return data.exists;
  }

  async uploadChunk(blob: Blob, hash: string, fileName: string): Promise<void> {
    const form = new FormData();
    form.append("hash", hash);
    form.append("file", blob, fileName);
    await this.axios().post(this.url("/files/chunks"), form, {
      headers: { "Content-Type": "multipart/form-data" },
    });
  }

  async createFileFromChunks(req: CreateFileFromChunksRequest): Promise<FileItem> {
    const { data } = await this.axios().post<FileItem>(
      this.url("/files"),
      req,
    );
    return data;
  }

  getDownloadUrl(fileId: string): string {
    return this.url(`/files/${encodeURIComponent(fileId)}/download`);
  }

  async listChildren(nodeId: string): Promise<ChildrenResponse> {
    const { data } = await this.axios().get<ChildrenResponse>(
      this.url(`/layout/${encodeURIComponent(nodeId)}/children`),
    );
    return data;
  }

  async createFolder(req: CreateFolderRequest): Promise<NodeItem> {
    const { data } = await this.axios().post<NodeItem>(
      this.url("/layout/folders"),
      req,
    );
    return data;
  }
}

export const filesApi = new FilesApiClient();
