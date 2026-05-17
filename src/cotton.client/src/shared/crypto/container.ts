import {
  CorruptedContainerError,
  InvalidCryptoInputError,
  NotAContainerError,
  UnsupportedVersionError,
} from "./errors";

export const MAGIC = new Uint8Array([
  0x43, 0x4f, 0x54, 0x45, 0x4e, 0x43, 0x31, 0x00,
]);
export const CONTAINER_VERSION = 1;
export const ALG_AES_256_GCM = 1;
export const GCM_TAG_BYTES = 16;
export const GCM_NONCE_PREFIX_BYTES = 8;
export const GCM_NONCE_BYTES = 12;
export const DEFAULT_CHUNK_SIZE = 4 * 1024 * 1024;
export const MAX_CHUNK_SIZE = 64 * 1024 * 1024;
export const MAX_CHUNK_COUNT = 0x1_0000_0000;

export interface ContainerHeader {
  containerVersion: number;
  contentAlgId: number;
  chunkSize: number;
  noncePrefix: Uint8Array;
  wrappedFileKey: Uint8Array;
  plaintextSize: number;
}

export function looksLikeContainer(bytes: Uint8Array): boolean {
  if (bytes.length < MAGIC.length) {
    return false;
  }

  for (let i = 0; i < MAGIC.length; i += 1) {
    if (bytes[i] !== MAGIC[i]) {
      return false;
    }
  }

  return true;
}

export function buildHeader(header: ContainerHeader): Uint8Array {
  assertSupportedHeader(header);

  const fixedPrefixLength = MAGIC.length + 1 + 1 + 4 + GCM_NONCE_PREFIX_BYTES + 2;
  const totalLength = fixedPrefixLength + header.wrappedFileKey.length + 8;
  const output = new Uint8Array(totalLength);
  const view = new DataView(output.buffer);
  let offset = 0;

  output.set(MAGIC, offset);
  offset += MAGIC.length;
  output[offset] = header.containerVersion;
  offset += 1;
  output[offset] = header.contentAlgId;
  offset += 1;
  view.setUint32(offset, header.chunkSize, false);
  offset += 4;
  output.set(header.noncePrefix, offset);
  offset += GCM_NONCE_PREFIX_BYTES;
  view.setUint16(offset, header.wrappedFileKey.length, false);
  offset += 2;
  output.set(header.wrappedFileKey, offset);
  offset += header.wrappedFileKey.length;

  const high = Math.floor(header.plaintextSize / 0x1_0000_0000);
  const low = header.plaintextSize >>> 0;
  view.setUint32(offset, high, false);
  view.setUint32(offset + 4, low, false);

  return output;
}

export function parseHeader(bytes: Uint8Array): {
  header: ContainerHeader;
  headerLength: number;
} {
  if (!looksLikeContainer(bytes)) {
    throw new NotAContainerError("Magic header mismatch.");
  }

  const fixedPrefixLength = MAGIC.length + 1 + 1 + 4 + GCM_NONCE_PREFIX_BYTES + 2;
  if (bytes.length < fixedPrefixLength) {
    throw new CorruptedContainerError("Header is truncated before wrapped key length.");
  }

  const view = new DataView(bytes.buffer, bytes.byteOffset, bytes.byteLength);
  let offset = MAGIC.length;
  const containerVersion = bytes[offset];
  offset += 1;
  const contentAlgId = bytes[offset];
  offset += 1;

  if (containerVersion !== CONTAINER_VERSION) {
    throw new UnsupportedVersionError(`Unsupported container version: ${containerVersion}.`);
  }

  if (contentAlgId !== ALG_AES_256_GCM) {
    throw new UnsupportedVersionError(`Unsupported content algorithm: ${contentAlgId}.`);
  }

  const chunkSize = view.getUint32(offset, false);
  offset += 4;

  if (!isValidChunkSize(chunkSize)) {
    throw new CorruptedContainerError(`Invalid chunk size: ${chunkSize}.`);
  }

  const noncePrefix = bytes.slice(offset, offset + GCM_NONCE_PREFIX_BYTES);
  offset += GCM_NONCE_PREFIX_BYTES;
  const wrappedFileKeyLength = view.getUint16(offset, false);
  offset += 2;

  if (offset + wrappedFileKeyLength + 8 > bytes.length) {
    throw new CorruptedContainerError("Header is truncated inside wrapped key or size.");
  }

  const wrappedFileKey = bytes.slice(offset, offset + wrappedFileKeyLength);
  offset += wrappedFileKeyLength;
  const high = view.getUint32(offset, false);
  const low = view.getUint32(offset + 4, false);
  offset += 8;
  const plaintextSize = high * 0x1_0000_0000 + low;

  if (!Number.isSafeInteger(plaintextSize)) {
    throw new CorruptedContainerError("Plaintext size exceeds JavaScript safe integer range.");
  }

  if (chunkCount(plaintextSize, chunkSize) > MAX_CHUNK_COUNT) {
    throw new CorruptedContainerError("Container requires more chunks than the nonce space allows.");
  }

  return {
    header: {
      containerVersion,
      contentAlgId,
      chunkSize,
      noncePrefix,
      wrappedFileKey,
      plaintextSize,
    },
    headerLength: offset,
  };
}

export function chunkNonce(noncePrefix: Uint8Array, chunkIndex: number): Uint8Array {
  if (noncePrefix.length !== GCM_NONCE_PREFIX_BYTES) {
    throw new InvalidCryptoInputError(
      `Nonce prefix must be ${GCM_NONCE_PREFIX_BYTES} bytes.`,
    );
  }

  if (!Number.isInteger(chunkIndex) || chunkIndex < 0 || chunkIndex > 0xffffffff) {
    throw new InvalidCryptoInputError("Chunk index must be a 32-bit unsigned integer.");
  }

  const nonce = new Uint8Array(GCM_NONCE_BYTES);
  const view = new DataView(nonce.buffer);
  nonce.set(noncePrefix, 0);
  view.setUint32(GCM_NONCE_PREFIX_BYTES, chunkIndex, false);
  return nonce;
}

export function chunkPlaintextLength(
  chunkIndex: number,
  chunkSize: number,
  plaintextSize: number,
): number {
  if (!Number.isInteger(chunkIndex) || chunkIndex < 0) {
    throw new InvalidCryptoInputError("Chunk index must be a non-negative integer.");
  }

  assertPlaintextShape(plaintextSize, chunkSize);

  const start = chunkIndex * chunkSize;
  if (start >= plaintextSize) {
    return 0;
  }

  return Math.min(chunkSize, plaintextSize - start);
}

export function chunkCount(plaintextSize: number, chunkSize: number): number {
  assertPlaintextShape(plaintextSize, chunkSize);

  if (plaintextSize === 0) {
    return 0;
  }

  return Math.ceil(plaintextSize / chunkSize);
}

function assertSupportedHeader(header: ContainerHeader): void {
  if (header.containerVersion !== CONTAINER_VERSION) {
    throw new InvalidCryptoInputError("Unsupported container version.");
  }

  if (header.contentAlgId !== ALG_AES_256_GCM) {
    throw new InvalidCryptoInputError("Unsupported content algorithm.");
  }

  if (!isValidChunkSize(header.chunkSize)) {
    throw new InvalidCryptoInputError("Chunk size is outside the supported range.");
  }

  if (header.noncePrefix.length !== GCM_NONCE_PREFIX_BYTES) {
    throw new InvalidCryptoInputError(
      `Nonce prefix must be ${GCM_NONCE_PREFIX_BYTES} bytes.`,
    );
  }

  if (header.wrappedFileKey.length <= 0 || header.wrappedFileKey.length > 0xffff) {
    throw new InvalidCryptoInputError("Wrapped file key length is invalid.");
  }

  assertPlaintextShape(header.plaintextSize, header.chunkSize);

  if (chunkCount(header.plaintextSize, header.chunkSize) > MAX_CHUNK_COUNT) {
    throw new InvalidCryptoInputError("Plaintext requires more chunks than supported.");
  }
}

function assertPlaintextShape(plaintextSize: number, chunkSize: number): void {
  if (
    !Number.isSafeInteger(plaintextSize) ||
    plaintextSize < 0 ||
    !isValidChunkSize(chunkSize)
  ) {
    throw new InvalidCryptoInputError("Invalid plaintext size or chunk size.");
  }
}

function isValidChunkSize(chunkSize: number): boolean {
  return (
    Number.isSafeInteger(chunkSize) &&
    chunkSize > 0 &&
    chunkSize <= MAX_CHUNK_SIZE
  );
}
