import { API_ENDPOINTS } from "../config.ts";
import api from "./http.ts";
import type { FileManifestDto } from "./files.ts";

export interface LayoutNodeDto {
  id: string;
  userLayoutId: string;
  parentId?: string | null;
  name: string;
  createdAt: string; // ISO date
  updatedAt: string; // ISO date
}

export interface LayoutChildrenDto {
  id: string; // node id requested
  nodes: LayoutNodeDto[];
  files: FileManifestDto[];
}

export async function resolvePath(path?: string): Promise<LayoutNodeDto> {
  if (path && path.length > 0) {
    // backend path param is encoded in the URL segment: /layouts/resolver/{path}
    const seg = encodeURI(path);
    const res = await api.get<LayoutNodeDto>(
      `${API_ENDPOINTS.layouts}/resolver/${seg}`,
    );
    return res.data;
  } else {
    const res = await api.get<LayoutNodeDto>(
      `${API_ENDPOINTS.layouts}/resolver`,
    );
    return res.data;
  }
}

export async function getNodeChildren(nodeId: string): Promise<LayoutChildrenDto> {
  const res = await api.get<LayoutChildrenDto>(
    `${API_ENDPOINTS.layouts}/nodes/${encodeURIComponent(nodeId)}/children`,
  );
  return res.data;
}
