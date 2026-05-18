import { asBufferSource } from "./bufferSource";
import { chunkNonce, GCM_TAG_BYTES } from "./container";
import { CorruptedContainerError } from "./errors";

const subtle = (): SubtleCrypto => globalThis.crypto.subtle;

export interface EncryptedChunk {
  ciphertext: Uint8Array;
  tag: Uint8Array;
}

export async function encryptChunk(
  fileKey: CryptoKey,
  noncePrefix: Uint8Array,
  chunkIndex: number,
  plaintext: Uint8Array,
  aad: Uint8Array,
): Promise<EncryptedChunk> {
  const iv = chunkNonce(noncePrefix, chunkIndex);
  const buffer = await subtle().encrypt(
    {
      name: "AES-GCM",
      iv: asBufferSource(iv),
      additionalData: asBufferSource(aad),
      tagLength: GCM_TAG_BYTES * 8,
    },
    fileKey,
    asBufferSource(plaintext),
  );
  const encrypted = new Uint8Array(buffer);

  return {
    ciphertext: encrypted.slice(0, encrypted.length - GCM_TAG_BYTES),
    tag: encrypted.slice(encrypted.length - GCM_TAG_BYTES),
  };
}

export async function decryptChunk(
  fileKey: CryptoKey,
  noncePrefix: Uint8Array,
  chunkIndex: number,
  ciphertext: Uint8Array,
  tag: Uint8Array,
  aad: Uint8Array,
): Promise<Uint8Array> {
  if (tag.length !== GCM_TAG_BYTES) {
    throw new CorruptedContainerError("Ciphertext chunk has an invalid GCM tag.");
  }

  const iv = chunkNonce(noncePrefix, chunkIndex);
  const input = new Uint8Array(ciphertext.length + tag.length);
  input.set(ciphertext, 0);
  input.set(tag, ciphertext.length);

  try {
    const buffer = await subtle().decrypt(
      {
        name: "AES-GCM",
        iv: asBufferSource(iv),
        additionalData: asBufferSource(aad),
        tagLength: GCM_TAG_BYTES * 8,
      },
      fileKey,
      asBufferSource(input),
    );
    return new Uint8Array(buffer);
  } catch {
    throw new CorruptedContainerError(`Chunk ${chunkIndex} failed authentication.`);
  }
}
