export const isImageFile = (fileName: string): boolean => {
  const extension = fileName.toLowerCase().split(".").pop() || "";
  const imageExtensions = ["jpg", "jpeg", "png", "gif", "webp", "bmp", "svg"];
  return imageExtensions.includes(extension);
};
