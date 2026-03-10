import heic2any from "heic2any";

const heicUrlCache = new Map<string, string>();

const HEIC_MIME_TYPES: ReadonlySet<string> = new Set([
  "image/heic",
  "image/heif",
  "image/heic-sequence",
  "image/heif-sequence",
]);

export const isHeicFile = (fileName: string): boolean => {
  return fileName.toLowerCase().endsWith(".heic");
};

const needsHeicConversion = (blob: Blob): boolean => {
  if (HEIC_MIME_TYPES.has(blob.type)) return true;
  return blob.type === "" || blob.type === "application/octet-stream";
};

export const convertHeicToJpeg = async (url: string): Promise<string> => {
  const cachedUrl = heicUrlCache.get(url);
  if (cachedUrl) {
    return cachedUrl;
  }

  const response = await fetch(url);
  const blob = await response.blob();

  // Server already converted to a web-friendly format (e.g. webp) - use as-is
  if (!needsHeicConversion(blob)) {
    const objectUrl = URL.createObjectURL(blob);
    heicUrlCache.set(url, objectUrl);
    return objectUrl;
  }

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
  if (!cachedUrl) {
    return;
  }

  URL.revokeObjectURL(cachedUrl);
  heicUrlCache.delete(url);
};