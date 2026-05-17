import { describe, expect, it } from "vitest";
import { decryptChunk, encryptChunk } from "./cipher";
import { CorruptedContainerError } from "./errors";
import { generateFileKey } from "./keys";

const noncePrefix = new Uint8Array([0xaa, 0xbb, 0xcc, 0xdd, 0xee, 0xff, 0x00, 0x11]);

describe("encryptChunk / decryptChunk", () => {
  it("round-trips arbitrary plaintext under the same key and index", async () => {
    const fileKey = await generateFileKey();
    const plaintext = new Uint8Array(1024);
    crypto.getRandomValues(plaintext);

    const ciphertext = await encryptChunk(fileKey, noncePrefix, 0, plaintext);
    const restored = await decryptChunk(fileKey, noncePrefix, 0, ciphertext);

    expect(ciphertext).toHaveLength(plaintext.length + 16);
    expect(Array.from(restored)).toEqual(Array.from(plaintext));
  });

  it("authenticates the chunk index", async () => {
    const fileKey = await generateFileKey();
    const ciphertext = await encryptChunk(fileKey, noncePrefix, 0, new Uint8Array([1, 2, 3]));

    await expect(decryptChunk(fileKey, noncePrefix, 1, ciphertext)).rejects.toBeInstanceOf(
      CorruptedContainerError,
    );
  });

  it("rejects a different file key", async () => {
    const firstKey = await generateFileKey();
    const secondKey = await generateFileKey();
    const ciphertext = await encryptChunk(firstKey, noncePrefix, 0, new Uint8Array([1]));

    await expect(decryptChunk(secondKey, noncePrefix, 0, ciphertext)).rejects.toBeInstanceOf(
      CorruptedContainerError,
    );
  });

  it("rejects tampered ciphertext", async () => {
    const fileKey = await generateFileKey();
    const ciphertext = await encryptChunk(fileKey, noncePrefix, 0, new Uint8Array([1, 2, 3]));
    ciphertext[2] ^= 0x01;

    await expect(decryptChunk(fileKey, noncePrefix, 0, ciphertext)).rejects.toBeInstanceOf(
      CorruptedContainerError,
    );
  });

  it("rejects ciphertext shorter than a GCM tag", async () => {
    const fileKey = await generateFileKey();

    await expect(
      decryptChunk(fileKey, noncePrefix, 0, new Uint8Array(8)),
    ).rejects.toBeInstanceOf(CorruptedContainerError);
  });
});
