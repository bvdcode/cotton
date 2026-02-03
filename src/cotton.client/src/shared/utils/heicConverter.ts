import heic2any from "heic2any";

const heicUrlCache = new Map<string, string>();

export const isHeicFile = (fileName: string): boolean => {
  return fileName.toLowerCase().endsWith(".heic");
};

export const convertHeicToJpeg = async (url: string): Promise<string> => {
  if (heicUrlCache.has(url)) {
    return heicUrlCache.get(url)!;
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
  if (heicUrlCache.has(url)) {
    URL.revokeObjectURL(heicUrlCache.get(url)!);
    heicUrlCache.delete(url);
  }
};
