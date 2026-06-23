import { argon2id } from "hash-wasm";
import { asBufferSource } from "./bufferSource";
import { GCM_TAG_BYTES } from "./container";
import { InvalidCryptoInputError, WrongUnlockError } from "./errors";

export interface Argon2idParams {
  memoryKiB: number;
  iterations: number;
  parallelism: number;
}

export const DEFAULT_ARGON2ID: Argon2idParams = {
  memoryKiB: 64 * 1024,
  iterations: 3,
  parallelism: 1,
};

export const MASTER_KEY_BYTES = 32;
export const FILE_KEY_BYTES = 32;
export const KDF_SALT_BYTES = 16;

export const MIN_ARGON2ID_MEMORY_KIB = 8;
export const MAX_ARGON2ID_MEMORY_KIB = 256 * 1024;
export const MAX_ARGON2ID_ITERATIONS = 10;
export const MAX_ARGON2ID_PARALLELISM = 4;

const subtle = (): SubtleCrypto => {
  const subtleCrypto = globalThis.crypto?.subtle;

  if (!subtleCrypto) {
    throw new InvalidCryptoInputError("WebCrypto is not available.");
  }

  return subtleCrypto;
};

const isSafePositiveInteger = (value: number): boolean =>
  Number.isSafeInteger(value) && value > 0;

export function isValidArgon2idParams(params: Argon2idParams): boolean {
  return (
    Number.isSafeInteger(params.memoryKiB) &&
    params.memoryKiB >= MIN_ARGON2ID_MEMORY_KIB &&
    params.memoryKiB <= MAX_ARGON2ID_MEMORY_KIB &&
    isSafePositiveInteger(params.iterations) &&
    params.iterations <= MAX_ARGON2ID_ITERATIONS &&
    isSafePositiveInteger(params.parallelism) &&
    params.parallelism <= MAX_ARGON2ID_PARALLELISM
  );
}

export function assertValidArgon2idParams(params: Argon2idParams): void {
  if (!isValidArgon2idParams(params)) {
    throw new InvalidCryptoInputError("Invalid Argon2id parameters.");
  }
}

export function randomBytes(length: number): Uint8Array {
  if (!Number.isSafeInteger(length) || length < 0) {
    throw new InvalidCryptoInputError(
      "Random byte length must be a non-negative integer.",
    );
  }

  const output = new Uint8Array(length);
  globalThis.crypto.getRandomValues(output);
  return output;
}

export async function deriveKek(
  passphrase: string,
  salt: Uint8Array,
  params: Argon2idParams,
): Promise<CryptoKey> {
  if (salt.length !== KDF_SALT_BYTES) {
    throw new InvalidCryptoInputError(
      `KDF salt must be ${KDF_SALT_BYTES} bytes.`,
    );
  }

  assertValidArgon2idParams(params);

  const raw = (await argon2id({
    password: passphrase,
    salt,
    parallelism: params.parallelism,
    iterations: params.iterations,
    memorySize: params.memoryKiB,
    hashLength: MASTER_KEY_BYTES,
    outputType: "binary",
  })) as Uint8Array;

  try {
    return await subtle().importKey(
      "raw",
      asBufferSource(raw),
      { name: "AES-KW" },
      false,
      ["wrapKey", "unwrapKey"],
    );
  } finally {
    raw.fill(0);
  }
}

export async function generateMasterKey(): Promise<CryptoKey> {
  return subtle().generateKey({ name: "AES-GCM", length: 256 }, true, [
    "encrypt",
    "decrypt",
  ]);
}

export async function exportMasterKey(
  masterKey: CryptoKey,
): Promise<Uint8Array> {
  const raw = new Uint8Array(await subtle().exportKey("raw", masterKey));

  if (raw.length !== MASTER_KEY_BYTES) {
    raw.fill(0);
    throw new InvalidCryptoInputError("Master key has an invalid length.");
  }

  return raw;
}

export async function importMasterKey(raw: Uint8Array): Promise<CryptoKey> {
  if (raw.length !== MASTER_KEY_BYTES) {
    throw new InvalidCryptoInputError("Master key has an invalid length.");
  }

  return subtle().importKey(
    "raw",
    asBufferSource(raw),
    { name: "AES-GCM", length: 256 },
    true,
    ["encrypt", "decrypt"],
  );
}

export async function generateFileKey(): Promise<CryptoKey> {
  return subtle().generateKey({ name: "AES-GCM", length: 256 }, true, [
    "encrypt",
    "decrypt",
  ]);
}

export async function wrapMasterKey(
  kek: CryptoKey,
  masterKey: CryptoKey,
): Promise<Uint8Array> {
  const buffer = await subtle().wrapKey("raw", masterKey, kek, {
    name: "AES-KW",
  });
  return new Uint8Array(buffer);
}

export async function unwrapMasterKey(
  kek: CryptoKey,
  wrapped: Uint8Array,
): Promise<CryptoKey> {
  try {
    return await subtle().unwrapKey(
      "raw",
      asBufferSource(wrapped),
      kek,
      { name: "AES-KW" },
      { name: "AES-GCM", length: 256 },
      true,
      ["encrypt", "decrypt"],
    );
  } catch {
    throw new WrongUnlockError("Master key unwrap failed.");
  }
}

export async function wrapFileKey(
  masterKey: CryptoKey,
  fileKey: CryptoKey,
  nonce: Uint8Array,
  aad: Uint8Array,
): Promise<{ encryptedFileKey: Uint8Array; tag: Uint8Array }> {
  const rawFileKey = new Uint8Array(await subtle().exportKey("raw", fileKey));

  try {
    const buffer = await subtle().encrypt(
      {
        name: "AES-GCM",
        iv: asBufferSource(nonce),
        additionalData: asBufferSource(aad),
        tagLength: GCM_TAG_BYTES * 8,
      },
      masterKey,
      asBufferSource(rawFileKey),
    );
    const encrypted = new Uint8Array(buffer);

    return {
      encryptedFileKey: encrypted.slice(0, encrypted.length - GCM_TAG_BYTES),
      tag: encrypted.slice(encrypted.length - GCM_TAG_BYTES),
    };
  } finally {
    rawFileKey.fill(0);
  }
}

export async function unwrapFileKey(
  masterKey: CryptoKey,
  encryptedFileKey: Uint8Array,
  tag: Uint8Array,
  nonce: Uint8Array,
  aad: Uint8Array,
): Promise<CryptoKey> {
  if (tag.length !== GCM_TAG_BYTES) {
    throw new InvalidCryptoInputError(
      "Wrapped file key tag has an invalid length.",
    );
  }

  const input = new Uint8Array(encryptedFileKey.length + tag.length);
  input.set(encryptedFileKey, 0);
  input.set(tag, encryptedFileKey.length);

  const rawFileKey = new Uint8Array(
    await subtle().decrypt(
      {
        name: "AES-GCM",
        iv: asBufferSource(nonce),
        additionalData: asBufferSource(aad),
        tagLength: GCM_TAG_BYTES * 8,
      },
      masterKey,
      asBufferSource(input),
    ),
  );

  try {
    return await subtle().importKey(
      "raw",
      asBufferSource(rawFileKey),
      { name: "AES-GCM", length: 256 },
      false,
      ["encrypt", "decrypt"],
    );
  } finally {
    rawFileKey.fill(0);
  }
}

export async function deriveMetadataKey(
  masterKey: CryptoKey,
): Promise<CryptoKey> {
  const rawMasterKey = new Uint8Array(
    await subtle().exportKey("raw", masterKey),
  );

  try {
    const hkdfKey = await subtle().importKey(
      "raw",
      asBufferSource(rawMasterKey),
      "HKDF",
      false,
      ["deriveKey"],
    );

    return await subtle().deriveKey(
      {
        name: "HKDF",
        hash: "SHA-256",
        salt: asBufferSource(new Uint8Array(0)),
        info: asBufferSource(
          new TextEncoder().encode("cotton:display-meta:v1"),
        ),
      },
      hkdfKey,
      { name: "AES-GCM", length: 256 },
      false,
      ["encrypt", "decrypt"],
    );
  } finally {
    rawMasterKey.fill(0);
  }
}
