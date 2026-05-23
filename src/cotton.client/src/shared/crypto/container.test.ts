import { describe, expect, it } from "vitest";
import {
  CHUNK_HEADER_BYTES,
  CONTAINER_VERSION,
  LEGACY_MAGIC,
  FILE_HEADER_BYTES,
  GCM_NONCE_BYTES,
  MAGIC,
  MAX_CHUNK_SIZE,
  buildChunkAad,
  buildChunkHeader,
  buildHeader,
  buildKeyAad,
  chunkCount,
  chunkNonce,
  chunkPlaintextLength,
  looksLikeContainer,
  parseChunkHeader,
  parseHeader,
  type ContainerHeader,
} from "./container";
import {
  CorruptedContainerError,
  InvalidCryptoInputError,
  NotAContainerError,
} from "./errors";

const sampleHeader: ContainerHeader = {
  formatVersion: CONTAINER_VERSION,
  keyId: 7,
  noncePrefix: new Uint8Array([1, 2, 3, 4]),
  fileKeyNonce: new Uint8Array(12).map((_, index) => 10 + index),
  fileKeyTag: new Uint8Array(16).map((_, index) => 30 + index),
  encryptedFileKey: new Uint8Array(32).map((_, index) => 60 + index),
  plaintextSize: 123_456_789,
};

describe("looksLikeContainer", () => {
  it("accepts the CTN2 magic prefix", () => {
    expect(looksLikeContainer(MAGIC)).toBe(true);
  });

  it("accepts the legacy CTN1 magic prefix", () => {
    expect(looksLikeContainer(LEGACY_MAGIC)).toBe(true);
  });

  it("accepts buffers that start with the magic prefix", () => {
    const bytes = new Uint8Array(32);
    bytes.set(MAGIC, 0);

    expect(looksLikeContainer(bytes)).toBe(true);
  });

  it("rejects short or unrelated buffers", () => {
    expect(looksLikeContainer(new Uint8Array(3))).toBe(false);
    expect(looksLikeContainer(new Uint8Array([1, 2, 3, 4]))).toBe(false);
  });
});

describe("buildHeader / parseHeader", () => {
  it("writes the current file header layout", () => {
    const bytes = buildHeader(sampleHeader);
    const view = new DataView(bytes.buffer, bytes.byteOffset, bytes.byteLength);
    const { header, headerLength } = parseHeader(bytes);

    expect(bytes).toHaveLength(FILE_HEADER_BYTES);
    expect(headerLength).toBe(FILE_HEADER_BYTES);
    expect(Array.from(bytes.slice(0, 4))).toEqual(Array.from(MAGIC));
    expect(view.getInt32(4, true)).toBe(FILE_HEADER_BYTES);
    expect(view.getBigInt64(8, true)).toBe(BigInt(sampleHeader.plaintextSize));
    expect(view.getInt32(16, true)).toBe(sampleHeader.keyId);
    expect(Array.from(bytes.slice(20, 24))).toEqual(
      Array.from(sampleHeader.noncePrefix),
    );
    expect(header).toEqual(sampleHeader);
  });

  it("stores plaintext sizes above 4 GiB as little-endian int64", () => {
    const bytes = buildHeader({ ...sampleHeader, plaintextSize: 5_000_000_000 });

    expect(parseHeader(bytes).header.plaintextSize).toBe(5_000_000_000);
  });

  it("stores empty files", () => {
    const bytes = buildHeader({ ...sampleHeader, plaintextSize: 0 });

    expect(parseHeader(bytes).header.plaintextSize).toBe(0);
  });
});

describe("parseHeader malformed input", () => {
  it("throws NotAContainerError for wrong magic", () => {
    const bytes = buildHeader(sampleHeader);
    bytes[0] = 0xff;

    expect(() => parseHeader(bytes)).toThrow(NotAContainerError);
  });

  it("throws CorruptedContainerError for an unknown header length", () => {
    const bytes = buildHeader(sampleHeader);
    new DataView(bytes.buffer).setInt32(4, FILE_HEADER_BYTES + 1, true);

    expect(() => parseHeader(bytes)).toThrow(CorruptedContainerError);
  });

  it("throws CorruptedContainerError for truncation", () => {
    expect(() => parseHeader(buildHeader(sampleHeader).slice(0, 8))).toThrow(
      CorruptedContainerError,
    );
    expect(() =>
      parseHeader(buildHeader(sampleHeader).slice(0, FILE_HEADER_BYTES - 1)),
    ).toThrow(CorruptedContainerError);
  });

  it("rejects malformed fixed-length fields", () => {
    expect(() =>
      buildHeader({
        ...sampleHeader,
        encryptedFileKey: new Uint8Array(31),
      }),
    ).toThrow(InvalidCryptoInputError);
  });
});

describe("key AAD", () => {
  it("binds immutable file header fields", () => {
    const aad = buildKeyAad(sampleHeader);
    const view = new DataView(aad.buffer, aad.byteOffset, aad.byteLength);

    expect(Array.from(aad.slice(0, 4))).toEqual(Array.from(MAGIC));
    expect(view.getInt32(4, true)).toBe(FILE_HEADER_BYTES);
    expect(view.getBigInt64(8, true)).toBe(BigInt(sampleHeader.plaintextSize));
    expect(view.getInt32(16, true)).toBe(sampleHeader.keyId);
    expect(Array.from(aad.slice(20, 24))).toEqual(
      Array.from(sampleHeader.noncePrefix),
    );
    expect(Array.from(aad.slice(24))).toEqual(Array.from(sampleHeader.fileKeyNonce));
  });
});

describe("chunk headers and AAD", () => {
  it("writes and parses chunk headers", () => {
    const tag = new Uint8Array(16).map((_, index) => 100 + index);
    const bytes = buildChunkHeader({
      keyId: 3,
      plaintextLength: 32_768,
      tag,
    });
    const view = new DataView(bytes.buffer, bytes.byteOffset, bytes.byteLength);
    const parsed = parseChunkHeader(bytes);

    expect(bytes).toHaveLength(CHUNK_HEADER_BYTES);
    expect(Array.from(bytes.slice(0, 4))).toEqual(Array.from(MAGIC));
    expect(view.getInt32(4, true)).toBe(CHUNK_HEADER_BYTES);
    expect(view.getBigInt64(8, true)).toBe(32_768n);
    expect(view.getInt32(16, true)).toBe(3);
    expect(parsed).toEqual({ keyId: 3, plaintextLength: 32_768, tag });
  });

  it("writes chunk AAD as magic/version/key/chunk/length/reserved", () => {
    const aad = buildChunkAad(9, 0x01020304, 65_536);
    const view = new DataView(aad.buffer, aad.byteOffset, aad.byteLength);

    expect(Array.from(aad.slice(0, 4))).toEqual(Array.from(MAGIC));
    expect(view.getInt32(4, true)).toBe(CONTAINER_VERSION);
    expect(view.getInt32(8, true)).toBe(9);
    expect(view.getBigInt64(12, true)).toBe(0x01020304n);
    expect(view.getBigInt64(20, true)).toBe(65_536n);
    expect(view.getInt32(28, true)).toBe(0);
  });
});

describe("chunk nonce and sizing helpers", () => {
  it("builds noncePrefix(4) plus chunkIndex(8 LE)", () => {
    const prefix = new Uint8Array([10, 20, 30, 40]);
    const nonce = chunkNonce(prefix, 0x1_0000_0000);

    expect(nonce).toHaveLength(GCM_NONCE_BYTES);
    expect(Array.from(nonce.slice(0, 4))).toEqual(Array.from(prefix));
    expect(Array.from(nonce.slice(4))).toEqual([0, 0, 0, 0, 1, 0, 0, 0]);
  });

  it("rejects invalid chunk indices", () => {
    expect(() => chunkNonce(new Uint8Array(4), 1.5)).toThrow(
      InvalidCryptoInputError,
    );
    expect(() => chunkNonce(new Uint8Array(4), -1)).toThrow(
      InvalidCryptoInputError,
    );
  });

  it("counts and sizes chunks", () => {
    const chunkSize = 8 * 1024;

    expect(chunkCount(0, chunkSize)).toBe(0);
    expect(chunkCount(chunkSize * 4, chunkSize)).toBe(4);
    expect(chunkCount(chunkSize * 4 + 1, chunkSize)).toBe(5);
    expect(chunkPlaintextLength(0, chunkSize, chunkSize * 2 + 5)).toBe(chunkSize);
    expect(chunkPlaintextLength(1, chunkSize, chunkSize * 2 + 5)).toBe(chunkSize);
    expect(chunkPlaintextLength(2, chunkSize, chunkSize * 2 + 5)).toBe(5);
    expect(chunkPlaintextLength(3, chunkSize, chunkSize * 2 + 5)).toBe(0);
  });

  it("rejects chunk sizes outside the compatible range", () => {
    expect(() => chunkCount(1, 1)).toThrow(InvalidCryptoInputError);
    expect(() => chunkCount(1, MAX_CHUNK_SIZE + 1)).toThrow(
      InvalidCryptoInputError,
    );
  });
});
