import { DISPLAY_META_KEY } from "./displayMeta";
import { decryptMetadata, encryptMetadata } from "./metadataCipher";
import { isJsonObject, type JsonValue } from "../types/json";
import { InvalidCryptoInputError } from "./errors";
import { useVault } from "./vault";

export interface FolderNameFields {
  name: string;
  metadata?: Record<string, string> | null;
}

export function hasEncryptedFolderName(
  metadata: FolderNameFields["metadata"],
): boolean {
  return Boolean(metadata?.[DISPLAY_META_KEY]);
}

export async function encryptFolderName(name: string): Promise<string> {
  const normalized = name.trim();
  if (!normalized) {
    throw new InvalidCryptoInputError("Folder name is required.");
  }
  return encryptMetadata(JSON.stringify({ n: normalized }));
}

export async function readFolderName(node: FolderNameFields): Promise<string> {
  const value = node.metadata?.[DISPLAY_META_KEY];
  if (!value) {
    return node.name;
  }
  const parsed: JsonValue = JSON.parse(await decryptMetadata(value));
  if (
    !isJsonObject(parsed) ||
    typeof parsed.n !== "string" ||
    !parsed.n.trim()
  ) {
    throw new InvalidCryptoInputError("Folder display metadata is invalid.");
  }
  return parsed.n;
}

const projections = new WeakMap<
  CryptoKey,
  WeakMap<object, Promise<FolderNameFields>>
>();

export async function applyDisplayMetaToNode<T extends FolderNameFields>(
  node: T,
): Promise<T> {
  const key = useVault.getState().masterKey;
  if (!key || !hasEncryptedFolderName(node.metadata)) {
    return node;
  }
  let cache = projections.get(key);
  if (!cache) {
    cache = new WeakMap();
    projections.set(key, cache);
  }
  let pending = cache.get(node);
  if (!pending) {
    pending = readFolderName(node)
      .then((name) => ({ ...node, name }))
      .catch(() => node);
    cache.set(node, pending);
  }
  const display = await pending;
  if (useVault.getState().masterKey !== key) {
    return node;
  }
  return { ...node, name: display.name };
}

export async function applyDisplayMetaToNodes<T extends FolderNameFields>(
  nodes: T[],
): Promise<T[]> {
  return Promise.all(nodes.map(applyDisplayMetaToNode));
}
