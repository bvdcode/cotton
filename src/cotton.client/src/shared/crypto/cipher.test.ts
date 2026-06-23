import { describe, expect, it } from "vitest";
import { decryptChunk, encryptChunk } from "./cipher";
import { CorruptedContainerError } from "./errors";
import { generateFileKey } from "./keys";
import { buildChunkAad } from "./container";

const noncePrefix = new Uint8Array([0xaa, 0xbb, 0xcc, 0xdd]);
const keyId = 1;

describe("encryptChunk / decryptChunk", () => {
  it("round-trips arbitrary plaintext under the same key and index", async () => {
    const fileKey = await generateFileKey();
    const plaintext = new Uint8Array(1024);
    crypto.getRandomValues(plaintext);
    const aad = buildChunkAad(keyId, 0, plaintext.length);

    const encrypted = await encryptChunk(
      fileKey,
      noncePrefix,
      0,
      plaintext,
      aad,
    );
    const restored = await decryptChunk(
      fileKey,
      noncePrefix,
      0,
      encrypted.ciphertext,
      encrypted.tag,
      aad,
    );

    expect(encrypted.ciphertext).toHaveLength(plaintext.length);
    expect(encrypted.tag).toHaveLength(16);
    expect(Array.from(restored)).toEqual(Array.from(plaintext));
  });

  it("authenticates the chunk index", async () => {
    const fileKey = await generateFileKey();
    const plaintext = new Uint8Array([1, 2, 3]);
    const encrypted = await encryptChunk(
      fileKey,
      noncePrefix,
      0,
      plaintext,
      buildChunkAad(keyId, 0, plaintext.length),
    );

    await expect(
      decryptChunk(
        fileKey,
        noncePrefix,
        1,
        encrypted.ciphertext,
        encrypted.tag,
        buildChunkAad(keyId, 1, plaintext.length),
      ),
    ).rejects.toBeInstanceOf(CorruptedContainerError);
  });

  it("rejects a different file key", async () => {
    const firstKey = await generateFileKey();
    const secondKey = await generateFileKey();
    const plaintext = new Uint8Array([1]);
    const aad = buildChunkAad(keyId, 0, plaintext.length);
    const encrypted = await encryptChunk(
      firstKey,
      noncePrefix,
      0,
      plaintext,
      aad,
    );

    await expect(
      decryptChunk(
        secondKey,
        noncePrefix,
        0,
        encrypted.ciphertext,
        encrypted.tag,
        aad,
      ),
    ).rejects.toBeInstanceOf(CorruptedContainerError);
  });

  it("rejects tampered ciphertext", async () => {
    const fileKey = await generateFileKey();
    const plaintext = new Uint8Array([1, 2, 3]);
    const aad = buildChunkAad(keyId, 0, plaintext.length);
    const encrypted = await encryptChunk(
      fileKey,
      noncePrefix,
      0,
      plaintext,
      aad,
    );
    const ciphertext = encrypted.ciphertext.slice();
    ciphertext[2] ^= 0x01;

    await expect(
      decryptChunk(fileKey, noncePrefix, 0, ciphertext, encrypted.tag, aad),
    ).rejects.toBeInstanceOf(CorruptedContainerError);
  });

  it("rejects an invalid GCM tag", async () => {
    const fileKey = await generateFileKey();

    await expect(
      decryptChunk(
        fileKey,
        noncePrefix,
        0,
        new Uint8Array([1]),
        new Uint8Array(8),
        buildChunkAad(keyId, 0, 1),
      ),
    ).rejects.toBeInstanceOf(CorruptedContainerError);
  });
});
