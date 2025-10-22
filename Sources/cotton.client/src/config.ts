export const API_BASE_URL = "http://localhost:5182";

export const API_ENDPOINTS = {
  settings: "/api/v1/settings",
  chunk: "/api/v1/chunks",
  files: "/api/v1/files",
  layoutsResolver: "/api/v1/layouts/resolver",
  layoutsNodes: "/api/v1/layouts/nodes",
} as const;

export const buildDownloadUrl = (fileManifestId: string) =>
  `${API_BASE_URL}${API_ENDPOINTS.files}/${fileManifestId}/download`;

// Default number of parallel chunk uploads
export const UPLOAD_CONCURRENCY_DEFAULT = 4;
