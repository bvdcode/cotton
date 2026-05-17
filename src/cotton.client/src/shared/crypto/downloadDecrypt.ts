import { filesApi } from "../api/filesApi";
import type { NodeFileManifestDto } from "../api/nodesApi";
import { decryptBlobToBlob } from "./fileCipher";
import { getOriginalContentType, isFileEncrypted } from "./metadataFlags";
import { requireMasterKey } from "./vault";

const FALLBACK_CONTENT_TYPE = "application/octet-stream";
const DOWNLOAD_BLOB_REVOKE_DELAY_MS = 60_000;

export interface ReadableFileHandle {
  url: string;
  mimeType: string;
  revoke: () => void;
}

export async function getReadableFileUrl(
  file: NodeFileManifestDto,
  expireAfterMinutes?: number,
): Promise<ReadableFileHandle> {
  if (!isFileEncrypted(file.metadata)) {
    const url = await filesApi.getDownloadLink(file.id, expireAfterMinutes);

    return {
      url,
      mimeType: file.contentType || FALLBACK_CONTENT_TYPE,
      revoke: () => {},
    };
  }

  const masterKey = requireMasterKey();
  const signedUrl = await filesApi.getDownloadLink(file.id, expireAfterMinutes);
  const ciphertext = await fetchEncryptedBlob(signedUrl);
  const originalContentType =
    getOriginalContentType(file.metadata) ||
    file.contentType ||
    FALLBACK_CONTENT_TYPE;
  const plaintext = await decryptBlobToBlob(
    ciphertext,
    masterKey,
    originalContentType,
  );
  const blobUrl = URL.createObjectURL(plaintext);

  return {
    url: blobUrl,
    mimeType: originalContentType,
    revoke: () => URL.revokeObjectURL(blobUrl),
  };
}

export async function downloadReadableFile(
  file: NodeFileManifestDto,
  fileName = file.name,
): Promise<void> {
  const handle = await getReadableFileUrl(file);

  try {
    triggerBrowserDownload(handle.url, fileName);
  } finally {
    window.setTimeout(handle.revoke, DOWNLOAD_BLOB_REVOKE_DELAY_MS);
  }
}

async function fetchEncryptedBlob(url: string): Promise<Blob> {
  const response = await fetch(url);

  if (!response.ok) {
    throw new Error(
      `Failed to fetch encrypted file: ${response.status} ${response.statusText}`,
    );
  }

  return await response.blob();
}

function triggerBrowserDownload(url: string, fileName: string): void {
  const link = document.createElement("a");
  link.href = url;
  link.download = fileName;
  link.rel = "noopener noreferrer";
  link.style.display = "none";

  if (!url.startsWith("blob:")) {
    link.target = "_blank";
  }

  document.body.appendChild(link);
  link.click();
  document.body.removeChild(link);
}
