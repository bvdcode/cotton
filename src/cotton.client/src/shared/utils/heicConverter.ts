import heic2any from "heic2any";

const heicUrlCache = new Map<string, string>();

export const isHeicFile = (fileName: string): boolean => {
  return fileName.toLowerCase().endsWith(".heic");
};

const canBrowserDecode = (url: string): Promise<boolean> =>
  new Promise((resolve) => {
    const img = new Image();
    img.onload = () => resolve(true);
    img.onerror = () => resolve(false);
    img.src = url;
  });

export const convertHeicToJpeg = async (url: string): Promise<string> => {
  const cachedUrl = heicUrlCache.get(url);
  if (cachedUrl) return cachedUrl;

  if (await canBrowserDecode(url)) {
    heicUrlCache.set(url, url);
    return url;
  }

  const response = await fetch(url);
  const blob = await response.blob();
  const converted = await heic2any({ blob, toType: "image/jpeg", quality: 0.92 });
  const resultBlob = Array.isArray(converted) ? converted[0] : converted;
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