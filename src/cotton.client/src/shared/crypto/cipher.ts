import { asBufferSource } from "./bufferSource";
import { chunkNonce, GCM_TAG_BYTES } from "./container";
import { CorruptedContainerError } from "./errors";

const subtle = (): SubtleCrypto => globalThis.crypto.subtle;

export async function encryptChunk(
  fileKey: CryptoKey,
  noncePrefix: Uint8Array,
  chunkIndex: number,
  plaintext: Uint8Array,
): Promise<Uint8Array> {
  const iv = chunkNonce(noncePrefix, chunkIndex);
  const buffer = await subtle().encrypt(
    { name: "AES-GCM", iv: asBufferSource(iv) },
    fileKey,
    asBufferSource(plaintext),
  );
  return new Uint8Array(buffer);
}

export async function decryptChunk(
  fileKey: CryptoKey,
  noncePrefix: Uint8Array,
  chunkIndex: number,
  ciphertext: Uint8Array,
): Promise<Uint8Array> {
  if (ciphertext.length < GCM_TAG_BYTES) {
    throw new CorruptedContainerError("Ciphertext chunk is too short for a GCM tag.");
  }

  const iv = chunkNonce(noncePrefix, chunkIndex);

  try {
    const buffer = await subtle().decrypt(
      { name: "AES-GCM", iv: asBufferSource(iv) },
      fileKey,
      asBufferSource(ciphertext),
    );
    return new Uint8Array(buffer);
  } catch {
    throw new CorruptedContainerError(`Chunk ${chunkIndex} failed authentication.`);
  }
}
