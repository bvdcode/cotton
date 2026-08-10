const PREVIEW_API_PATH = "/api/v1/preview";

export const buildPreviewUrl = (token: string): string =>
  `${PREVIEW_API_PATH}/${encodeURIComponent(token)}.webp`;
