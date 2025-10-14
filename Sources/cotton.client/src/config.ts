export const API_BASE_URL = "http://localhost:5182";

export const API_ENDPOINTS = {
  settings: "/api/v1/settings",
  chunk: "/api/v1/chunks",
  files: "/api/v1/files",
} as const;

export const buildDownloadUrl = (fileManifestId: string) =>
  `${API_BASE_URL}${API_ENDPOINTS.files}/${fileManifestId}/download`;
