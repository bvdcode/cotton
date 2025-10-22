export const API_BASE_URL = "http://localhost:5182";

export const API_ENDPOINTS = {
  auth: "/api/v1/auth",
  files: "/api/v1/files",
  chunk: "/api/v1/chunks",
  layouts: "/api/v1/layouts",
  settings: "/api/v1/settings",
} as const;

export const buildDownloadUrl = (fileManifestId: string) =>
  `${API_BASE_URL}${API_ENDPOINTS.files}/${fileManifestId}/download`;

// Default number of parallel chunk uploads
export const UPLOAD_CONCURRENCY_DEFAULT = 4;
