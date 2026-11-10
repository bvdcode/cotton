import type { NodeFileManifestDto } from "../api/nodesApi";
import { isJsonObject, type JsonValue } from "../types/json";
import { InvalidCryptoInputError } from "./errors";
import { isFileEncrypted } from "./metadataFlags";
import { useVault } from "./vault";
import { encryptMetadata, decryptMetadata } from "./metadataCipher";

export const DISPLAY_META_KEY = "en";

export interface DisplayMeta {
  name: string;
  contentType: string;
}

type FileDisplayMetaFields = Pick<
  NodeFileManifestDto,
  "name" | "contentType" | "metadata"
>;

export async function encryptDisplayMeta(meta: DisplayMeta): Promise<string> {
  const normalized = normalizeDisplayMeta(meta);
  return encryptMetadata(
    JSON.stringify({ n: normalized.name, c: normalized.contentType }),
  );
}

export async function decryptDisplayMeta(value: string): Promise<DisplayMeta> {
  return parseDisplayMeta(await decryptMetadata(value));
}

export async function readFileDisplayMeta(
  file: FileDisplayMetaFields,
): Promise<DisplayMeta> {
  if (!isFileEncrypted(file.metadata)) {
    return { name: file.name, contentType: file.contentType };
  }
  const value = file.metadata[DISPLAY_META_KEY];
  if (!value) {
    throw new InvalidCryptoInputError(
      "Encrypted file display metadata is missing.",
    );
  }
  return decryptDisplayMeta(value);
}

export async function applyDisplayMetaToFile<
  TFile extends FileDisplayMetaFields,
>(file: TFile): Promise<TFile> {
  if (!isFileEncrypted(file.metadata)) {
    return file;
  }

  const encryptedDisplayMeta = file.metadata?.[DISPLAY_META_KEY];
  if (!encryptedDisplayMeta || !useVault.getState().isUnlocked) {
    return file;
  }

  try {
    const displayMeta = await decryptDisplayMeta(encryptedDisplayMeta);
    return {
      ...file,
      name: displayMeta.name,
      contentType: displayMeta.contentType,
    };
  } catch {
    return file;
  }
}

export async function applyDisplayMetaToFiles(
  files: NodeFileManifestDto[],
): Promise<NodeFileManifestDto[]> {
  if (!useVault.getState().isUnlocked || files.length === 0) {
    return files;
  }

  const decorated = await Promise.all(files.map(applyDisplayMetaToFile));
  return decorated.some((file, index) => file !== files[index])
    ? decorated
    : files;
}

function normalizeDisplayMeta(meta: DisplayMeta): DisplayMeta {
  const name = meta.name.trim();
  const contentType = meta.contentType.trim();

  if (name.length === 0) {
    throw new InvalidCryptoInputError("Display metadata name is required.");
  }

  if (contentType.length === 0) {
    throw new InvalidCryptoInputError(
      "Display metadata content type is required.",
    );
  }

  return { name, contentType };
}

function parseDisplayMeta(value: string): DisplayMeta {
  const parsed: JsonValue = JSON.parse(value);

  if (!isJsonObject(parsed)) {
    throw new InvalidCryptoInputError("Display metadata must be an object.");
  }

  if (typeof parsed.n !== "string" || typeof parsed.c !== "string") {
    throw new InvalidCryptoInputError("Display metadata shape is invalid.");
  }

  return normalizeDisplayMeta({
    name: parsed.n,
    contentType: parsed.c,
  });
}
