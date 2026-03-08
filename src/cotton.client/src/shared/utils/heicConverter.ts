import heic2any from "heic2any";

const heicUrlCache = new Map<string, string>();

export const isHeicFile = (fileName: string): boolean => {
  return fileName.toLowerCase().endsWith(".heic");
};

export const convertHeicToJpeg = async (url: string): Promise<string> => {
  const cachedUrl = heicUrlCache.get(url);
  if (cachedUrl) {
    return cachedUrl;
  }

  const response = await fetch(url);
  const heicBlob = await response.blob();

  const jpegBlob = await heic2any({
    blob: heicBlob,
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