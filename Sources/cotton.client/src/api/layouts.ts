import { API_BASE_URL, API_ENDPOINTS } from "../config.ts";
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
    const res = await fetch(`${API_BASE_URL}${API_ENDPOINTS.layouts}/resolver/${seg}`);
    if (!res.ok) throw new Error(`Resolve failed: ${res.status}`);
    return (await res.json()) as LayoutNodeDto;
  } else {
    const res = await fetch(`${API_BASE_URL}${API_ENDPOINTS.layouts}/resolver`);
    if (!res.ok) throw new Error(`Resolve failed: ${res.status}`);
    return (await res.json()) as LayoutNodeDto;
  }
}

export async function getNodeChildren(nodeId: string): Promise<LayoutChildrenDto> {
  const res = await fetch(
    `${API_BASE_URL}${API_ENDPOINTS.layouts}/nodes/${encodeURIComponent(nodeId)}/children`,
  );
  if (!res.ok) throw new Error(`Children fetch failed: ${res.status}`);
  const data = (await res.json()) as LayoutChildrenDto;
  return data;
}
