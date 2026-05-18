import type { NodeFileManifestDto } from "../api/nodesApi";
import { asBufferSource } from "./bufferSource";
import { base64ToBytes, bytesToBase64 } from "./base64";
import { InvalidCryptoInputError } from "./errors";
import { isFileEncrypted } from "./metadataFlags";
import { randomBytes } from "./keys";
import { requireMetadataKey, useVault } from "./vault";

export const DISPLAY_META_KEY = "en";

const NONCE_BYTES = 12;
const encoder = new TextEncoder();
const decoder = new TextDecoder();
const OPAQUE_FILE_VALUES = Symbol("cotton.opaqueFileValues");

export interface DisplayMeta {
  name: string;
  contentType: string;
}

type FileDisplayMetaFields = Pick<
  NodeFileManifestDto,
  "name" | "contentType" | "metadata"
>;

type FileWithOpaqueValues<TFile extends FileDisplayMetaFields> = TFile & {
  [OPAQUE_FILE_VALUES]?: Pick<NodeFileManifestDto, "name" | "contentType">;
};

export async function encryptDisplayMeta(meta: DisplayMeta): Promise<string> {
  const normalized = normalizeDisplayMeta(meta);
  const metadataKey = await requireMetadataKey();
  const iv = randomBytes(NONCE_BYTES);
  const plaintext = encoder.encode(
    JSON.stringify({
      n: normalized.name,
      c: normalized.contentType,
    }),
  );
  const ciphertext = new Uint8Array(
    await subtle().encrypt(
      { name: "AES-GCM", iv: asBufferSource(iv) },
      metadataKey,
      asBufferSource(plaintext),
    ),
  );
  const payload = new Uint8Array(iv.length + ciphertext.length);

  payload.set(iv, 0);
  payload.set(ciphertext, iv.length);

  return bytesToBase64(payload);
}

export async function decryptDisplayMeta(value: string): Promise<DisplayMeta> {
  const metadataKey = await requireMetadataKey();
  const payload = base64ToBytes(value);

  if (payload.length <= NONCE_BYTES) {
    throw new InvalidCryptoInputError("Display metadata payload is too short.");
  }

  const iv = payload.slice(0, NONCE_BYTES);
  const ciphertext = payload.slice(NONCE_BYTES);
  const plaintext = await subtle().decrypt(
    { name: "AES-GCM", iv: asBufferSource(iv) },
    metadataKey,
    asBufferSource(ciphertext),
  );

  return parseDisplayMeta(decoder.decode(plaintext));
}

export async function applyDisplayMetaToFile<TFile extends FileDisplayMetaFields>(
  file: TFile,
): Promise<TFile> {
  if (!isFileEncrypted(file.metadata)) {
    return file;
  }

  const encryptedDisplayMeta = file.metadata?.[DISPLAY_META_KEY];
  if (!encryptedDisplayMeta || !useVault.getState().isUnlocked) {
    return file;
  }

  try {
    const displayMeta = await decryptDisplayMeta(encryptedDisplayMeta);
    const opaque = (file as FileWithOpaqueValues<TFile>)[OPAQUE_FILE_VALUES] ?? {
      name: file.name,
      contentType: file.contentType,
    };
    const decorated: FileWithOpaqueValues<TFile> = {
      ...file,
      name: displayMeta.name,
      contentType: displayMeta.contentType,
    } as FileWithOpaqueValues<TFile>;
    Object.defineProperty(decorated, OPAQUE_FILE_VALUES, {
      value: opaque,
      enumerable: false,
      configurable: false,
    });

    return decorated;
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

export function toPersistableFileDisplayMetadata(
  file: NodeFileManifestDto,
): NodeFileManifestDto | null {
  if (!isFileEncrypted(file.metadata)) {
    return file;
  }

  const opaque = (file as FileWithOpaqueValues<NodeFileManifestDto>)[
    OPAQUE_FILE_VALUES
  ];
  if (opaque) {
    return {
      ...file,
      name: opaque.name,
      contentType: opaque.contentType,
    };
  }

  return useVault.getState().isUnlocked ? null : file;
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
  const parsed = JSON.parse(value) as unknown;

  if (!parsed || typeof parsed !== "object") {
    throw new InvalidCryptoInputError("Display metadata must be an object.");
  }

  const candidate = parsed as { n?: unknown; c?: unknown };
  if (typeof candidate.n !== "string" || typeof candidate.c !== "string") {
    throw new InvalidCryptoInputError("Display metadata shape is invalid.");
  }

  return normalizeDisplayMeta({
    name: candidate.n,
    contentType: candidate.c,
  });
}

function subtle(): SubtleCrypto {
  const subtleCrypto = globalThis.crypto?.subtle;

  if (!subtleCrypto) {
    throw new InvalidCryptoInputError("WebCrypto is not available.");
  }

  return subtleCrypto;
}
