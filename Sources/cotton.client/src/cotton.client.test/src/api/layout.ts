import api from "./http.ts";
import { API_ENDPOINTS } from "../config.ts";

export interface CreateFolderRequest {
  parentId: string;
  name: string;
}

export async function createFolder(req: CreateFolderRequest): Promise<void> {
  const res = await api.put(`${API_ENDPOINTS.layouts}/nodes`, req);
  if (res.status < 200 || res.status >= 300) {
    throw new Error(`Create folder failed: ${res.status}`);
  }
}

export default {
  createFolder,
};
