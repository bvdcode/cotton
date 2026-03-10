import heic2any from "heic2any";

const heicUrlCache = new Map<string, string>();

const HEIC_CONTENT_TYPES = ["image/heic", "image/heif", "image/heic-sequence", "image/heif-sequence"];

export const isHeicFile = (fileName: string): boolean => {
  return fileName.toLowerCase().endsWith(".heic");
};

const isHeicContentType = (contentType: string): boolean => {
  const normalized = contentType.split(";")[0].trim().toLowerCase();
  return HEIC_CONTENT_TYPES.includes(normalized) || normalized === "" || normalized === "application/octet-stream";
};

export const convertHeicToJpeg = async (url: string): Promise<string> => {
  const cachedUrl = heicUrlCache.get(url);
  if (cachedUrl) {
    return cachedUrl;
  }

  const headResponse = await fetch(url, { method: "HEAD" });
  const contentType = headResponse.headers.get("content-type") ?? "";

  if (!isHeicContentType(contentType)) {
    heicUrlCache.set(url, url);
    return url;
  }

  const response = await fetch(url);
  const blob = await response.blob();
  const jpegBlob = await heic2any({
    blob,
    toType: "image/jpeg",
    quality: 0.92,
  });

  const resultBlob = Array.isArray(jpegBlob) ? jpegBlob[0] : jpegBlob;
  const objectUrl = URL.createObjectURL(resultBlob);

  heicUrlCache.set(url, objectUrl);

  return objectUrl;
};

export const cleanupHeicUrl = (url: string): void => {
  const cachedUrl = heicUrlCache.get(url);
  if (!cachedUrl || cachedUrl === url) {
    heicUrlCache.delete(url);
    return;
  }

  URL.revokeObjectURL(cachedUrl);
  heicUrlCache.delete(url);
};