import { filesApi, type MoveFileRequest } from "../api/filesApi";
import type { NodeFileManifestDto } from "../api/nodesApi";
import {
  DISPLAY_META_KEY,
  encryptDisplayMeta,
  readFileDisplayMeta,
} from "../crypto/displayMeta";
import { isFileEncrypted } from "../crypto/metadataFlags";
import { requireMasterKey } from "../crypto/vault";
import type {
  FileTransferOperation,
  MoveClipboardItem,
} from "../store/moveClipboardStore";
import { createOpaqueServerFileName } from "../upload/uploadFileToNode";
import { FolderTransferNames } from "./FolderTransferNames";

export async function transferFile(
  item: MoveClipboardItem,
  targetParentId: string,
  name: string | undefined,
  overwrite: boolean,
  operation: FileTransferOperation,
  names: FolderTransferNames,
): Promise<NodeFileManifestDto> {
  const request: MoveFileRequest = { parentId: targetParentId };
  let displayName = name ?? item.file?.name;
  if (isFileEncrypted(item.file?.metadata)) {
    if (!item.file) {
      throw new Error("File metadata is missing from the transfer item.");
    }
    const key = requireMasterKey();
    const meta = await readFileDisplayMeta(item.file);
    displayName = name ?? meta.name;
    const existingName = await names.check(
      displayName,
      operation === "move" ? item.id : undefined,
      overwrite,
    );
    if (existingName !== undefined) {
      request.name = existingName;
    } else if (operation === "copy") {
      request.name = createOpaqueServerFileName();
    }
    if (name !== undefined) {
      request.metadata = {
        [DISPLAY_META_KEY]: await encryptDisplayMeta({
          ...meta,
          name: displayName,
        }),
      };
    }
    if (requireMasterKey() !== key) {
      throw new Error(
        "The encryption key changed while preparing the file transfer.",
      );
    }
  } else if (name !== undefined) {
    request.name = name;
  }
  if (overwrite) {
    request.overwrite = true;
  }
  let file: NodeFileManifestDto;
  switch (operation) {
    case "move":
      file = await filesApi.moveFile(item.id, request);
      break;
    case "copy":
      file = await filesApi.copyFile(item.id, request);
      break;
  }
  names.record(
    displayName ?? file.name,
    file.id,
    "File",
    file.name,
    isFileEncrypted(file.metadata),
  );
  return file;
}
