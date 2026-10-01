import { isRawImageFile } from "./fileTypes";

export const resolveImageDisplayUrl = (
  url: string,
  preferPreview: boolean,
  fileName: string,
  contentType?: string | null,
): string => {
  if (url.startsWith("blob:") || url.startsWith("data:")) {
    return url;
  }

  const usePreview = preferPreview || isRawImageFile(fileName, contentType);
  const parsed = new URL(url, window.location.origin);
  parsed.searchParams.set("preview", String(usePreview));
  return URL.canParse(url)
    ? parsed.toString()
    : `${parsed.pathname}${parsed.search}${parsed.hash}`;
};
