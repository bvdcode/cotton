import { describe, expect, it } from "vitest";
import {
  ALG_AES_256_GCM,
  CONTAINER_VERSION,
  GCM_NONCE_BYTES,
  MAGIC,
  MAX_CHUNK_COUNT,
  buildHeader,
  chunkCount,
  chunkNonce,
  chunkPlaintextLength,
  looksLikeContainer,
  parseHeader,
} from "./container";
import {
  CorruptedContainerError,
  InvalidCryptoInputError,
  NotAContainerError,
  UnsupportedVersionError,
} from "./errors";

const sampleHeader = {
  containerVersion: CONTAINER_VERSION,
  contentAlgId: ALG_AES_256_GCM,
  chunkSize: 4 * 1024 * 1024,
  noncePrefix: new Uint8Array([1, 2, 3, 4, 5, 6, 7, 8]),
  wrappedFileKey: new Uint8Array(40).map((_, index) => index + 1),
  plaintextSize: 123_456_789,
};

describe("looksLikeContainer", () => {
  it("accepts the canonical magic prefix", () => {
    expect(looksLikeContainer(MAGIC)).toBe(true);
  });

  it("accepts buffers that start with the magic prefix", () => {
    const bytes = new Uint8Array(32);
    bytes.set(MAGIC, 0);

    expect(looksLikeContainer(bytes)).toBe(true);
  });

  it("rejects short or unrelated buffers", () => {
    expect(looksLikeContainer(new Uint8Array(3))).toBe(false);
    expect(looksLikeContainer(new Uint8Array([1, 2, 3, 4, 5, 6, 7, 8]))).toBe(false);
  });
});

describe("buildHeader / parseHeader", () => {
  it("preserves every field", () => {
    const bytes = buildHeader(sampleHeader);
    const { header, headerLength } = parseHeader(bytes);

    expect(headerLength).toBe(bytes.length);
    expect(header.containerVersion).toBe(sampleHeader.containerVersion);
    expect(header.contentAlgId).toBe(sampleHeader.contentAlgId);
    expect(header.chunkSize).toBe(sampleHeader.chunkSize);
    expect(header.plaintextSize).toBe(sampleHeader.plaintextSize);
    expect(Array.from(header.noncePrefix)).toEqual(Array.from(sampleHeader.noncePrefix));
    expect(Array.from(header.wrappedFileKey)).toEqual(
      Array.from(sampleHeader.wrappedFileKey),
    );
  });

  it("stores plaintext sizes above 4 GiB", () => {
    const bytes = buildHeader({ ...sampleHeader, plaintextSize: 5_000_000_000 });

    expect(parseHeader(bytes).header.plaintextSize).toBe(5_000_000_000);
  });

  it("stores empty files", () => {
    const bytes = buildHeader({ ...sampleHeader, plaintextSize: 0 });

    expect(parseHeader(bytes).header.plaintextSize).toBe(0);
  });

  it("rejects a header that exceeds the chunk nonce space", () => {
    expect(() =>
      buildHeader({
        ...sampleHeader,
        chunkSize: 1,
        plaintextSize: MAX_CHUNK_COUNT + 1,
      }),
    ).toThrow(InvalidCryptoInputError);
  });
});

describe("parseHeader malformed input", () => {
  it("throws NotAContainerError for wrong magic", () => {
    const bytes = buildHeader(sampleHeader);
    bytes[0] = 0xff;

    expect(() => parseHeader(bytes)).toThrow(NotAContainerError);
  });

  it("throws UnsupportedVersionError for unknown version or algorithm", () => {
    const version = buildHeader(sampleHeader);
    version[MAGIC.length] = 99;
    const algorithm = buildHeader(sampleHeader);
    algorithm[MAGIC.length + 1] = 99;

    expect(() => parseHeader(version)).toThrow(UnsupportedVersionError);
    expect(() => parseHeader(algorithm)).toThrow(UnsupportedVersionError);
  });

  it("throws CorruptedContainerError for truncation", () => {
    expect(() => parseHeader(buildHeader(sampleHeader).slice(0, MAGIC.length + 5))).toThrow(
      CorruptedContainerError,
    );
    expect(() => parseHeader(buildHeader(sampleHeader).slice(0, MAGIC.length + 16))).toThrow(
      CorruptedContainerError,
    );
  });
});

describe("chunk nonce and sizing helpers", () => {
  it("builds noncePrefix(8) plus chunkIndex(4 BE)", () => {
    const prefix = new Uint8Array([10, 20, 30, 40, 50, 60, 70, 80]);
    const nonce = chunkNonce(prefix, 0x01020304);

    expect(nonce).toHaveLength(GCM_NONCE_BYTES);
    expect(Array.from(nonce.slice(0, 8))).toEqual(Array.from(prefix));
    expect(Array.from(nonce.slice(8))).toEqual([0x01, 0x02, 0x03, 0x04]);
  });

  it("rejects invalid chunk indices", () => {
    expect(() => chunkNonce(new Uint8Array(8), 1.5)).toThrow(InvalidCryptoInputError);
    expect(() => chunkNonce(new Uint8Array(8), -1)).toThrow(InvalidCryptoInputError);
  });

  it("counts and sizes chunks", () => {
    expect(chunkCount(0, 10)).toBe(0);
    expect(chunkCount(40, 10)).toBe(4);
    expect(chunkCount(41, 10)).toBe(5);
    expect(chunkPlaintextLength(0, 10, 25)).toBe(10);
    expect(chunkPlaintextLength(1, 10, 25)).toBe(10);
    expect(chunkPlaintextLength(2, 10, 25)).toBe(5);
    expect(chunkPlaintextLength(3, 10, 25)).toBe(0);
  });
});
