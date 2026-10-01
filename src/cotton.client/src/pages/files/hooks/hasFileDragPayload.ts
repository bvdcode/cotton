export const hasFileDragPayload = (
  dataTransfer: DataTransfer | null,
): boolean => {
  if (!dataTransfer) {
    return false;
  }

  if (dataTransfer.items && dataTransfer.items.length > 0) {
    return Array.from(dataTransfer.items).some((item) => item.kind === "file");
  }

  if (dataTransfer.types && dataTransfer.types.length > 0) {
    return Array.from(dataTransfer.types).includes("Files");
  }

  return Boolean(dataTransfer.files && dataTransfer.files.length > 0);
};
