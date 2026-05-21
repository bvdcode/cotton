import {
  archiveApi,
  type CreateArchiveDownloadLinkRequest,
} from "../api/archiveApi";
import { filesApi } from "../api/filesApi";

/**
 * Download a file by creating a temporary download link
 */
export const downloadFile = async (
  nodeFileId: string,
  fileName: string,
): Promise<void> => {
  try {
    const downloadLink = await filesApi.getDownloadLink(nodeFileId);
    openDownloadLink(downloadLink, fileName);
  } catch (error) {
    console.error("Failed to download file:", error);
    throw error;
  }
};

export const downloadArchive = async (
  request: CreateArchiveDownloadLinkRequest,
): Promise<void> => {
  try {
    const archive = await archiveApi.createDownloadLink(request);
    openDownloadLink(archive.url, archive.fileName);
  } catch (error) {
    console.error("Failed to download archive:", error);
    throw error;
  }
};

export const openDownloadLink = (href: string, fileName: string): void => {
  const link = document.createElement("a");
  link.href = href;
  link.download = fileName;
  link.target = "_blank";
  link.rel = "noopener noreferrer";
  link.style.display = "none";
  document.body.appendChild(link);
  link.click();
  document.body.removeChild(link);
};
