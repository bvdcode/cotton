import type { AxiosInstance } from "axios";
import { getHttpOrThrow } from "./http";
import type { CreateFolderRequest, LayoutChildrenDto, LayoutNodeDto } from "../types/api";

const BASE = "/api/v1/layouts";

class LayoutApiClient {
  private axios(): AxiosInstance {
    return getHttpOrThrow();
  }

  async resolvePath(path?: string): Promise<LayoutNodeDto> {
    if (path && path.length > 0) {
      const seg = encodeURI(path);
      const { data } = await this.axios().get<LayoutNodeDto>(`${BASE}/resolver/${seg}`);
      return data;
    }
    const { data } = await this.axios().get<LayoutNodeDto>(`${BASE}/resolver`);
    return data;
  }

  async getNode(nodeId: string): Promise<LayoutNodeDto> {
    const { data } = await this.axios().get<LayoutNodeDto>(`${BASE}/nodes/${encodeURIComponent(nodeId)}`);
    return data;
  }

  async getNodeChildren(nodeId: string): Promise<LayoutChildrenDto> {
    const { data } = await this.axios().get<LayoutChildrenDto>(`${BASE}/nodes/${encodeURIComponent(nodeId)}/children`);
    return data;
  }

  async createFolder(req: CreateFolderRequest): Promise<void> {
    await this.axios().put(`${BASE}/nodes`, req);
  }
}

export const layoutApi = new LayoutApiClient();
export type { LayoutApiClient };
