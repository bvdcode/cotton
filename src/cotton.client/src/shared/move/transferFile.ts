import { filesApi, type MoveFileRequest } from "../api/filesApi";
import type { NodeFileManifestDto } from "../api/nodesApi";
import {
  DISPLAY_META_KEY,
  encryptDisplayMeta,
  readFileDisplayMeta,
} from "../crypto/displayMeta";
import { isFileEncrypted } from "../crypto/metadataFlags";
import { requireMasterKey, useVault } from "../crypto/vault";
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
  const displayName = isFileEncrypted(item.file?.metadata)
    ? await prepareEncryptedTransfer(
        item,
        request,
        name,
        overwrite,
        operation,
        names,
      )
    : await preparePlainTransfer(
        item,
        request,
        name,
        overwrite,
        operation,
        names,
      );
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

async function prepareEncryptedTransfer(
  item: MoveClipboardItem,
  request: MoveFileRequest,
  name: string | undefined,
  overwrite: boolean,
  operation: FileTransferOperation,
  names: FolderTransferNames,
): Promise<string> {
  if (!item.file) {
    throw new Error("File metadata is missing from the transfer item.");
  }
  const key = requireMasterKey();
  const meta = await readFileDisplayMeta(item.file);
  const displayName = name ?? meta.name;
  const existingName = await names.check(
    displayName,
    getSourceFileId(item, operation),
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
  return displayName;
}

async function preparePlainTransfer(
  item: MoveClipboardItem,
  request: MoveFileRequest,
  name: string | undefined,
  overwrite: boolean,
  operation: FileTransferOperation,
  names: FolderTransferNames,
): Promise<string | undefined> {
  const displayName = name ?? item.file?.name;
  if (useVault.getState().isUnlocked && displayName !== undefined) {
    const existingName = await names.check(
      displayName,
      getSourceFileId(item, operation),
      overwrite,
      false,
    );
    if (existingName !== undefined) {
      request.name = existingName;
    }
  }
  if (name !== undefined) {
    request.name = name;
  }
  return displayName;
}

function getSourceFileId(
  item: MoveClipboardItem,
  operation: FileTransferOperation,
): string | undefined {
  switch (operation) {
    case "move":
      return item.id;
    case "copy":
      return undefined;
  }
}
