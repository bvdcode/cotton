import { describe, expect, it } from "vitest";
import { asBufferSource } from "./bufferSource";
import { encryptChunk } from "./cipher";
import {
  CHUNK_HEADER_BYTES,
  DEFAULT_KEY_ID,
  FILE_HEADER_BYTES,
  buildChunkAad,
  buildChunkHeader,
  buildHeader,
  buildKeyAad,
  parseChunkHeader,
  parseHeader,
} from "./container";
import { CorruptedContainerError } from "./errors";
import { decryptBlobToBlob } from "./fileCipher";
import { importMasterKey, wrapFileKey } from "./keys";

const GOLDEN_CONTAINER_HEX = [
  "43544e315400000020000000000000000100000001020304101112131415161718191a1b",
  "105a66b023aeb952c3b16a92055c1160dd5f3ab5ed6c9c1462dca2b6a3d4c7fc",
  "67e1fcbdaf77e1065f4058dce2e9ea64",
  "43544e3124000000100000000000000001000000063de420397031cdab567aa89be7f18d",
  "86d0074dab5ecff2aa2da23f6adee0ef",
  "43544e3124000000100000000000000001000000c5ef24a09009a9ed0ccc6edd0daa845f",
  "92687f5a3ed888916cd851a46a61f41e",
].join("");
const EASY_EXTENSIONS_SINGLE_CHUNK_HEX = [
  "43544e315400000020000000000000000100000001020304101112131415161718191a1b",
  "105a66b023aeb952c3b16a92055c1160dd5f3ab5ed6c9c1462dca2b6a3d4c7fc",
  "67e1fcbdaf77e1065f4058dce2e9ea64",
  "43544e3124000000200000000000000001000000f7cae02a9b0ba5f0b205f417676e555a",
  "86d0074dab5ecff2aa2da23f6adee0ef2bf2fe613fd6de9c493e03d29e28cda1",
].join("");

const GOLDEN_PLAINTEXT = new TextEncoder().encode(
  "0123456789abcdefABCDEFGHIJKLMNOP",
);
const GOLDEN_CHUNK_SIZE = 16;
const MASTER_KEY_BYTES = Uint8Array.from(
  Array.from({ length: 32 }, (_, index) => index),
);
const FILE_KEY_BYTES = Uint8Array.from(
  Array.from({ length: 32 }, (_, index) => 0xa0 + index),
);
const NONCE_PREFIX = new Uint8Array([1, 2, 3, 4]);
const FILE_KEY_NONCE = Uint8Array.from(
  Array.from({ length: 12 }, (_, index) => 0x10 + index),
);

describe("CTN1 golden vectors", () => {
  it("decrypts the fixed EasyExtensions-compatible container", async () => {
    const masterKey = await importMasterKey(MASTER_KEY_BYTES);
    const decrypted = await decryptBlobToBlob(
      new Blob([hexToBytes(GOLDEN_CONTAINER_HEX) as BlobPart]),
      masterKey,
      "text/plain",
    );

    expect(bytesToHex(await blobToBytes(decrypted))).toBe(
      bytesToHex(GOLDEN_PLAINTEXT),
    );
    expect(decrypted.type).toBe("text/plain");
  });

  it("decrypts the single-chunk EasyExtensions fixture", async () => {
    const masterKey = await importMasterKey(MASTER_KEY_BYTES);
    const decrypted = await decryptBlobToBlob(
      new Blob([hexToBytes(EASY_EXTENSIONS_SINGLE_CHUNK_HEX) as BlobPart]),
      masterKey,
      "text/plain",
    );

    expect(bytesToHex(await blobToBytes(decrypted))).toBe(
      bytesToHex(GOLDEN_PLAINTEXT),
    );
  });

  it("rebuilds the fixed container byte-for-byte", async () => {
    const bytes = await buildGoldenContainer();

    expect(bytesToHex(bytes)).toBe(GOLDEN_CONTAINER_HEX);
  });

  it("keeps file and chunk header boundaries stable", () => {
    const bytes = hexToBytes(GOLDEN_CONTAINER_HEX);
    const { header, headerLength } = parseHeader(bytes);
    const firstChunkHeader = parseChunkHeader(
      bytes.slice(FILE_HEADER_BYTES, FILE_HEADER_BYTES + CHUNK_HEADER_BYTES),
    );
    const secondChunkOffset =
      FILE_HEADER_BYTES + CHUNK_HEADER_BYTES + GOLDEN_CHUNK_SIZE;
    const secondChunkHeader = parseChunkHeader(
      bytes.slice(secondChunkOffset, secondChunkOffset + CHUNK_HEADER_BYTES),
    );

    expect(headerLength).toBe(FILE_HEADER_BYTES);
    expect(header).toMatchObject({
      keyId: DEFAULT_KEY_ID,
      plaintextSize: GOLDEN_PLAINTEXT.length,
    });
    expect(Array.from(header.noncePrefix)).toEqual(Array.from(NONCE_PREFIX));
    expect(Array.from(header.fileKeyNonce)).toEqual(Array.from(FILE_KEY_NONCE));
    expect(firstChunkHeader).toMatchObject({
      keyId: DEFAULT_KEY_ID,
      plaintextLength: GOLDEN_CHUNK_SIZE,
    });
    expect(secondChunkHeader).toMatchObject({
      keyId: DEFAULT_KEY_ID,
      plaintextLength: GOLDEN_CHUNK_SIZE,
    });
  });

  it.each([
    {
      name: "file plaintext length",
      mutate: (bytes: Uint8Array) => {
        new DataView(bytes.buffer, bytes.byteOffset, bytes.byteLength).setBigInt64(
          8,
          BigInt(GOLDEN_PLAINTEXT.length - 1),
          true,
        );
      },
    },
    {
      name: "file key nonce",
      mutate: (bytes: Uint8Array) => {
        bytes[24] ^= 0x01;
      },
    },
    {
      name: "chunk plaintext length",
      mutate: (bytes: Uint8Array) => {
        new DataView(bytes.buffer, bytes.byteOffset, bytes.byteLength).setBigInt64(
          FILE_HEADER_BYTES + 8,
          BigInt(GOLDEN_CHUNK_SIZE - 1),
          true,
        );
      },
    },
    {
      name: "chunk key id",
      mutate: (bytes: Uint8Array) => {
        bytes[FILE_HEADER_BYTES + 16] ^= 0x01;
      },
    },
    {
      name: "chunk ciphertext",
      mutate: (bytes: Uint8Array) => {
        bytes[FILE_HEADER_BYTES + CHUNK_HEADER_BYTES] ^= 0x01;
      },
    },
  ])("rejects tampered $name", async ({ mutate }) => {
    const masterKey = await importMasterKey(MASTER_KEY_BYTES);
    const tampered = hexToBytes(GOLDEN_CONTAINER_HEX);
    mutate(tampered);

    await expect(
      decryptBlobToBlob(new Blob([tampered as BlobPart]), masterKey),
    ).rejects.toBeInstanceOf(CorruptedContainerError);
  });

  it.each([
    {
      name: "swapped chunks",
      build: () => {
        const bytes = hexToBytes(GOLDEN_CONTAINER_HEX);
        const chunkRecordBytes = CHUNK_HEADER_BYTES + GOLDEN_CHUNK_SIZE;
        return concatBytes(
          bytes.slice(0, FILE_HEADER_BYTES),
          bytes.slice(
            FILE_HEADER_BYTES + chunkRecordBytes,
            FILE_HEADER_BYTES + chunkRecordBytes * 2,
          ),
          bytes.slice(FILE_HEADER_BYTES, FILE_HEADER_BYTES + chunkRecordBytes),
        );
      },
    },
    {
      name: "duplicated first chunk",
      build: () => {
        const bytes = hexToBytes(GOLDEN_CONTAINER_HEX);
        const chunkRecordBytes = CHUNK_HEADER_BYTES + GOLDEN_CHUNK_SIZE;
        const firstChunk = bytes.slice(
          FILE_HEADER_BYTES,
          FILE_HEADER_BYTES + chunkRecordBytes,
        );
        return concatBytes(bytes.slice(0, FILE_HEADER_BYTES), firstChunk, firstChunk);
      },
    },
  ])("rejects $name", async ({ build }) => {
    const masterKey = await importMasterKey(MASTER_KEY_BYTES);

    await expect(
      decryptBlobToBlob(new Blob([build() as BlobPart]), masterKey),
    ).rejects.toBeInstanceOf(CorruptedContainerError);
  });
});

async function buildGoldenContainer(): Promise<Uint8Array> {
  const masterKey = await importMasterKey(MASTER_KEY_BYTES);
  const fileKey = await crypto.subtle.importKey(
    "raw",
    asBufferSource(FILE_KEY_BYTES),
    { name: "AES-GCM", length: 256 },
    true,
    ["encrypt", "decrypt"],
  );
  const keyHeader = {
    keyId: DEFAULT_KEY_ID,
    noncePrefix: NONCE_PREFIX,
    fileKeyNonce: FILE_KEY_NONCE,
    plaintextSize: GOLDEN_PLAINTEXT.length,
  };
  const wrappedFileKey = await wrapFileKey(
    masterKey,
    fileKey,
    FILE_KEY_NONCE,
    buildKeyAad(keyHeader),
  );
  const parts = [
    buildHeader({
      ...keyHeader,
      fileKeyTag: wrappedFileKey.tag,
      encryptedFileKey: wrappedFileKey.encryptedFileKey,
    }),
  ];

  for (let chunkIndex = 0; chunkIndex < 2; chunkIndex += 1) {
    const plaintextChunk = GOLDEN_PLAINTEXT.slice(
      chunkIndex * GOLDEN_CHUNK_SIZE,
      (chunkIndex + 1) * GOLDEN_CHUNK_SIZE,
    );
    const encryptedChunk = await encryptChunk(
      fileKey,
      NONCE_PREFIX,
      chunkIndex,
      plaintextChunk,
      buildChunkAad(DEFAULT_KEY_ID, chunkIndex, plaintextChunk.length),
    );

    parts.push(
      buildChunkHeader({
        keyId: DEFAULT_KEY_ID,
        plaintextLength: plaintextChunk.length,
        tag: encryptedChunk.tag,
      }),
      encryptedChunk.ciphertext,
    );
  }

  return concatBytes(...parts);
}

function hexToBytes(hex: string): Uint8Array {
  const bytes = new Uint8Array(hex.length / 2);

  for (let index = 0; index < bytes.length; index += 1) {
    bytes[index] = Number.parseInt(hex.slice(index * 2, index * 2 + 2), 16);
  }

  return bytes;
}

function bytesToHex(bytes: Uint8Array): string {
  return Array.from(bytes, (byte) => byte.toString(16).padStart(2, "0")).join("");
}

async function blobToBytes(blob: Blob): Promise<Uint8Array> {
  return new Uint8Array(await blob.arrayBuffer());
}

function concatBytes(...parts: Uint8Array[]): Uint8Array {
  const output = new Uint8Array(
    parts.reduce((length, part) => length + part.length, 0),
  );
  let offset = 0;

  for (const part of parts) {
    output.set(part, offset);
    offset += part.length;
  }

  return output;
}
