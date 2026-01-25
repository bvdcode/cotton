import { filesApi } from "../../../shared/api/filesApi";

/**
 * Download a file by creating a temporary download link
 */
export const downloadFile = async (
  nodeFileId: string,
  fileName: string,
): Promise<void> => {
  try {
    const downloadLink = await filesApi.getDownloadLink(nodeFileId);
    const link = document.createElement("a");
    link.href = downloadLink;
    link.download = fileName;
    link.target = "_blank";
    link.rel = "noopener noreferrer";
    link.style.display = "none";
    document.body.appendChild(link);
    link.click();
    document.body.removeChild(link);
  } catch (error) {
    console.error("Failed to download file:", error);
    throw error;
  }
};
