import type {
  AuthUser,
  LayoutNodeDto,
  FileManifestDto,
  CreateFileRequest,
  LayoutChildrenDto,
  NodeType,
} from "../types/api";
import type { AxiosInstance } from "axios";

export class ApiService {
  private readonly getAxios: () => AxiosInstance;
  private readonly base = "/api/v1";

  constructor(getAxios: () => AxiosInstance) {
    this.getAxios = getAxios;
  }

  // Files
  async chunkExists(hash: string): Promise<boolean> {
    const axios = this.getAxios();
    try {
      await axios.get(`${this.base}/chunks/${encodeURIComponent(hash)}`);
      return true;
    } catch (err: unknown) {
      // 404 means chunk doesn't exist - expected behavior
      if (
        err &&
        typeof err === "object" &&
        "response" in err &&
        err.response &&
        typeof err.response === "object" &&
        "status" in err.response &&
        err.response.status === 404
      ) {
        return false;
      }
      // Other errors (including 401) should be thrown to trigger interceptors
      throw err;
    }
  }

  async uploadChunk(
    blob: Blob,
    hash: string,
    fileName?: string,
  ): Promise<void> {
    const axios = this.getAxios();
    const form = new FormData();
    form.append("file", blob, fileName ?? "chunk.bin");
    form.append("hash", hash);
    const res = await axios.post(`${this.base}/chunks`, form);
    if (res.status < 200 || res.status >= 300) {
      throw new Error(`Chunk upload failed: ${res.status}`);
    }
  }

  async createFileFromChunks(req: CreateFileRequest): Promise<FileManifestDto> {
    type Envelope = {
      success: boolean;
      message: string;
      data: FileManifestDto | null;
    };
    type Resp = Envelope | FileManifestDto;
    const axios = this.getAxios();
    const { data } = await axios.post<Resp>(
      `${this.base}/files/from-chunks`,
      req,
    );
    if (typeof (data as { success?: unknown }).success === "boolean") {
      const env = data as Envelope;
      if (!env.success || !env.data)
        throw new Error(env.message || "Empty response");
      return env.data;
    }
    return data as FileManifestDto;
  }

  getDownloadUrl(fileId: string): string {
    return `${this.base}/files/${encodeURIComponent(fileId)}/download`;
  }

  async deleteFile(nodeFileId: string): Promise<void> {
    const axios = this.getAxios();
    await axios.delete(`${this.base}/files/${encodeURIComponent(nodeFileId)}`);
  }

  // Layout
  async resolvePath(
    path?: string,
    nodeType?: NodeType,
  ): Promise<LayoutNodeDto> {
    const axios = this.getAxios();
    if (path && path.length > 0) {
      const seg = encodeURI(path);
      const { data } = await axios.get<LayoutNodeDto>(
        `${this.base}/layouts/resolver/${seg}`,
        { params: nodeType !== undefined ? { nodeType } : undefined },
      );
      return data;
    }
    const { data } = await axios.get<LayoutNodeDto>(
      `${this.base}/layouts/resolver`,
      { params: nodeType !== undefined ? { nodeType } : undefined },
    );
    return data;
  }

  async getNode(nodeId: string): Promise<LayoutNodeDto> {
    const axios = this.getAxios();
    const { data } = await axios.get<LayoutNodeDto>(
      `${this.base}/layouts/nodes/${encodeURIComponent(nodeId)}`,
    );
    return data;
  }

  async getAncestors(
    nodeId: string,
    nodeType?: NodeType,
  ): Promise<LayoutNodeDto[]> {
    const axios = this.getAxios();
    const { data } = await axios.get<LayoutNodeDto[]>(
      `${this.base}/layouts/nodes/${encodeURIComponent(nodeId)}/ancestors`,
      { params: nodeType !== undefined ? { nodeType } : undefined },
    );
    return data;
  }

  async getNodeChildren(
    nodeId: string,
    nodeType?: NodeType,
  ): Promise<LayoutChildrenDto> {
    const axios = this.getAxios();
    const { data } = await axios.get<LayoutChildrenDto>(
      `${this.base}/layouts/nodes/${encodeURIComponent(nodeId)}/children`,
      { params: nodeType !== undefined ? { nodeType } : undefined },
    );
    return data;
  }

  async createFolder(req: { parentId: string; name: string }): Promise<void> {
    const axios = this.getAxios();
    await axios.put(`${this.base}/layouts/nodes`, req);
  }

  async deleteNode(nodeId: string): Promise<void> {
    const axios = this.getAxios();
    await axios.delete(
      `${this.base}/layouts/nodes/${encodeURIComponent(nodeId)}`,
    );
  }

  // Users
  async getMe(): Promise<AuthUser> {
    const axios = this.getAxios();
    const { data } = await axios.get<AuthUser>(`${this.base}/users/me`);
    return data;
  }
}
