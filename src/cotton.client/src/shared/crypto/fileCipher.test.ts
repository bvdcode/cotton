import { describe, expect, it } from "vitest";
import {
  decryptBlobToBlob,
  ENCRYPTED_CONTENT_TYPE,
  encryptFileToBlob,
} from "./fileCipher";
import { CorruptedContainerError, NotAContainerError } from "./errors";
import { generateMasterKey } from "./keys";
import { CHUNK_HEADER_BYTES, FILE_HEADER_BYTES, MAGIC } from "./container";

async function blobToBytes(blob: Blob): Promise<Uint8Array> {
  return new Uint8Array(await blob.arrayBuffer());
}

function fillRandom(bytes: Uint8Array): void {
  const maxBytesPerCall = 65_536;

  for (let offset = 0; offset < bytes.length; offset += maxBytesPerCall) {
    crypto.getRandomValues(
      bytes.subarray(offset, Math.min(offset + maxBytesPerCall, bytes.length)),
    );
  }
}

describe("encryptFileToBlob / decryptBlobToBlob", () => {
  it("round-trips a single-chunk blob", async () => {
    const masterKey = await generateMasterKey();
    const plaintext = new Uint8Array(1024);
    crypto.getRandomValues(plaintext);
    const source = new Blob([plaintext], { type: "text/plain" });

    const encrypted = await encryptFileToBlob(source, masterKey, 8192);
    const decrypted = await decryptBlobToBlob(encrypted, masterKey, "text/plain");

    expect(encrypted.type).toBe(ENCRYPTED_CONTENT_TYPE);
    expect(encrypted.size).toBeGreaterThan(plaintext.byteLength);
    expect(decrypted.type).toBe("text/plain");
    expect(Array.from(await blobToBytes(decrypted))).toEqual(Array.from(plaintext));
  });

  it("writes CTN1 file and chunk headers", async () => {
    const masterKey = await generateMasterKey();
    const plaintext = new Uint8Array(1024);
    crypto.getRandomValues(plaintext);

    const encrypted = await encryptFileToBlob(new Blob([plaintext]), masterKey, 8192);
    const bytes = await blobToBytes(encrypted);

    expect(Array.from(bytes.slice(0, 4))).toEqual(Array.from(MAGIC));
    expect(new DataView(bytes.buffer).getInt32(4, true)).toBe(FILE_HEADER_BYTES);
    expect(Array.from(bytes.slice(FILE_HEADER_BYTES, FILE_HEADER_BYTES + 4))).toEqual(
      Array.from(MAGIC),
    );
    expect(new DataView(bytes.buffer).getInt32(FILE_HEADER_BYTES + 4, true)).toBe(
      CHUNK_HEADER_BYTES,
    );
  });

  it("round-trips multi-chunk content", async () => {
    const masterKey = await generateMasterKey();
    const plaintext = new Uint8Array(70_000);
    fillRandom(plaintext);

    const encrypted = await encryptFileToBlob(new Blob([plaintext]), masterKey, 32_768);
    const decrypted = await decryptBlobToBlob(encrypted, masterKey);

    expect(Array.from(await blobToBytes(decrypted))).toEqual(Array.from(plaintext));
  });

  it("reports encrypt and decrypt chunk progress", async () => {
    const masterKey = await generateMasterKey();
    const plaintext = new Uint8Array(20_000);
    fillRandom(plaintext);
    const encryptedProgress: number[] = [];
    const decryptedProgress: number[] = [];

    const encrypted = await encryptFileToBlob(
      new Blob([plaintext]),
      masterKey,
      8192,
      {
        onProgress: (bytesProcessed, bytesTotal) => {
          expect(bytesTotal).toBe(plaintext.byteLength);
          encryptedProgress.push(bytesProcessed);
        },
      },
    );
    await decryptBlobToBlob(
      encrypted,
      masterKey,
      ENCRYPTED_CONTENT_TYPE,
      {
        onProgress: (bytesProcessed, bytesTotal) => {
          expect(bytesTotal).toBe(plaintext.byteLength);
          decryptedProgress.push(bytesProcessed);
        },
      },
    );

    expect(encryptedProgress).toEqual([0, 8192, 16384, 20000]);
    expect(decryptedProgress).toEqual([0, 8192, 16384, 20000]);
  });

  it("round-trips an empty blob", async () => {
    const masterKey = await generateMasterKey();

    const encrypted = await encryptFileToBlob(new Blob([]), masterKey, 8192);
    const decrypted = await decryptBlobToBlob(encrypted, masterKey);

    expect(decrypted.size).toBe(0);
    expect(encrypted.size).toBe(FILE_HEADER_BYTES);
  });

  it("rejects a non-container blob", async () => {
    const masterKey = await generateMasterKey();

    await expect(
      decryptBlobToBlob(new Blob([new Uint8Array(16)]), masterKey),
    ).rejects.toBeInstanceOf(NotAContainerError);
  });

  it("rejects tampered ciphertext", async () => {
    const masterKey = await generateMasterKey();
    const plaintext = new Uint8Array(32_000);
    fillRandom(plaintext);
    const encrypted = await encryptFileToBlob(new Blob([plaintext]), masterKey, 8192);
    const bytes = await blobToBytes(encrypted);
    bytes[bytes.length - 20] ^= 0x01;

    await expect(
      decryptBlobToBlob(new Blob([bytes as BlobPart]), masterKey),
    ).rejects.toBeInstanceOf(CorruptedContainerError);
  });

  it("rejects truncation and trailing bytes", async () => {
    const masterKey = await generateMasterKey();
    const encrypted = await encryptFileToBlob(
      new Blob([new Uint8Array([1, 2, 3, 4])]),
      masterKey,
      8192,
    );
    const bytes = await blobToBytes(encrypted);
    const truncated = bytes.slice(0, bytes.length - 1);
    const extended = new Uint8Array(bytes.length + 1);
    extended.set(bytes);

    await expect(
      decryptBlobToBlob(new Blob([truncated as BlobPart]), masterKey),
    ).rejects.toBeInstanceOf(CorruptedContainerError);
    await expect(
      decryptBlobToBlob(new Blob([extended as BlobPart]), masterKey),
    ).rejects.toBeInstanceOf(CorruptedContainerError);
  });

  it("rejects decryption with a different master key", async () => {
    const firstMasterKey = await generateMasterKey();
    const secondMasterKey = await generateMasterKey();
    const encrypted = await encryptFileToBlob(
      new Blob([new Uint8Array([1, 2, 3])]),
      firstMasterKey,
      8192,
    );

    await expect(
      decryptBlobToBlob(encrypted, secondMasterKey),
    ).rejects.toBeInstanceOf(CorruptedContainerError);
  });
});
