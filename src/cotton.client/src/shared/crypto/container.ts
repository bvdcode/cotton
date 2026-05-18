import {
  CorruptedContainerError,
  InvalidCryptoInputError,
  NotAContainerError,
} from "./errors";

export const MAGIC = new Uint8Array([0x43, 0x54, 0x4e, 0x31]);
export const CONTAINER_VERSION = 1;
export const ALG_AES_256_GCM = 1;
export const DEFAULT_KEY_ID = 1;
export const GCM_TAG_BYTES = 16;
export const GCM_NONCE_PREFIX_BYTES = 4;
export const GCM_NONCE_BYTES = 12;
const FILE_KEY_BYTES = 32;
export const FILE_HEADER_BYTES =
  MAGIC.length +
  4 +
  8 +
  4 +
  GCM_NONCE_PREFIX_BYTES +
  GCM_NONCE_BYTES +
  GCM_TAG_BYTES +
  FILE_KEY_BYTES;
export const CHUNK_HEADER_BYTES = MAGIC.length + 4 + 8 + 4 + GCM_TAG_BYTES;
export const DEFAULT_CHUNK_SIZE = 1 * 1024 * 1024;
export const MIN_CHUNK_SIZE = 8 * 1024;
export const MAX_CHUNK_SIZE = 64 * 1024 * 1024;
export const MAX_CHUNK_COUNT = Number.MAX_SAFE_INTEGER;

export interface ContainerHeader {
  keyId: number;
  noncePrefix: Uint8Array;
  fileKeyNonce: Uint8Array;
  fileKeyTag: Uint8Array;
  encryptedFileKey: Uint8Array;
  plaintextSize: number;
}

export interface ChunkHeader {
  keyId: number;
  plaintextLength: number;
  tag: Uint8Array;
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

  const output = new Uint8Array(FILE_HEADER_BYTES);
  const view = new DataView(output.buffer);
  let offset = 0;

  output.set(MAGIC, offset);
  offset += MAGIC.length;
  view.setInt32(offset, FILE_HEADER_BYTES, true);
  offset += 4;
  setInt64LittleEndian(view, offset, header.plaintextSize);
  offset += 8;
  view.setInt32(offset, header.keyId, true);
  offset += 4;
  output.set(header.noncePrefix, offset);
  offset += GCM_NONCE_PREFIX_BYTES;
  output.set(header.fileKeyNonce, offset);
  offset += GCM_NONCE_BYTES;
  output.set(header.fileKeyTag, offset);
  offset += GCM_TAG_BYTES;
  output.set(header.encryptedFileKey, offset);

  return output;
}

export function parseHeader(bytes: Uint8Array): {
  header: ContainerHeader;
  headerLength: number;
} {
  if (!looksLikeContainer(bytes)) {
    throw new NotAContainerError("Magic header mismatch.");
  }

  if (bytes.length < FILE_HEADER_BYTES) {
    throw new CorruptedContainerError("File header is truncated.");
  }

  const view = viewFor(bytes);
  const headerLength = view.getInt32(MAGIC.length, true);

  if (headerLength !== FILE_HEADER_BYTES) {
    throw new CorruptedContainerError("Unsupported file header length.");
  }

  let offset = MAGIC.length + 4;
  const plaintextSize = getSafeInt64LittleEndian(view, offset, "Plaintext size");
  offset += 8;
  const keyId = view.getInt32(offset, true);
  offset += 4;
  const noncePrefix = bytes.slice(offset, offset + GCM_NONCE_PREFIX_BYTES);
  offset += GCM_NONCE_PREFIX_BYTES;
  const fileKeyNonce = bytes.slice(offset, offset + GCM_NONCE_BYTES);
  offset += GCM_NONCE_BYTES;
  const fileKeyTag = bytes.slice(offset, offset + GCM_TAG_BYTES);
  offset += GCM_TAG_BYTES;
  const encryptedFileKey = bytes.slice(offset, offset + FILE_KEY_BYTES);

  const header: ContainerHeader = {
    keyId,
    noncePrefix,
    fileKeyNonce,
    fileKeyTag,
    encryptedFileKey,
    plaintextSize,
  };
  assertParsedHeader(header);

  return {
    header,
    headerLength,
  };
}

export function buildKeyAad(header: Pick<
  ContainerHeader,
  "keyId" | "noncePrefix" | "fileKeyNonce" | "plaintextSize"
>): Uint8Array {
  assertKeyId(header.keyId);
  assertByteLength(header.noncePrefix, GCM_NONCE_PREFIX_BYTES, "Nonce prefix");
  assertByteLength(header.fileKeyNonce, GCM_NONCE_BYTES, "File key nonce");
  assertPlaintextSize(header.plaintextSize);

  const output = new Uint8Array(
    MAGIC.length + 4 + 8 + 4 + GCM_NONCE_PREFIX_BYTES + GCM_NONCE_BYTES,
  );
  const view = new DataView(output.buffer);
  let offset = 0;

  output.set(MAGIC, offset);
  offset += MAGIC.length;
  view.setInt32(offset, FILE_HEADER_BYTES, true);
  offset += 4;
  setInt64LittleEndian(view, offset, header.plaintextSize);
  offset += 8;
  view.setInt32(offset, header.keyId, true);
  offset += 4;
  output.set(header.noncePrefix, offset);
  offset += GCM_NONCE_PREFIX_BYTES;
  output.set(header.fileKeyNonce, offset);

  return output;
}

export function buildChunkHeader(header: ChunkHeader): Uint8Array {
  assertKeyId(header.keyId);
  assertPlaintextLength(header.plaintextLength);
  assertByteLength(header.tag, GCM_TAG_BYTES, "Chunk tag");

  const output = new Uint8Array(CHUNK_HEADER_BYTES);
  const view = new DataView(output.buffer);
  let offset = 0;

  output.set(MAGIC, offset);
  offset += MAGIC.length;
  view.setInt32(offset, CHUNK_HEADER_BYTES, true);
  offset += 4;
  setInt64LittleEndian(view, offset, header.plaintextLength);
  offset += 8;
  view.setInt32(offset, header.keyId, true);
  offset += 4;
  output.set(header.tag, offset);

  return output;
}

export function parseChunkHeader(bytes: Uint8Array): ChunkHeader {
  if (!looksLikeContainer(bytes)) {
    throw new CorruptedContainerError("Chunk header magic mismatch.");
  }

  if (bytes.length < CHUNK_HEADER_BYTES) {
    throw new CorruptedContainerError("Chunk header is truncated.");
  }

  const view = viewFor(bytes);
  const headerLength = view.getInt32(MAGIC.length, true);

  if (headerLength !== CHUNK_HEADER_BYTES) {
    throw new CorruptedContainerError("Unsupported chunk header length.");
  }

  const plaintextLength = getSafeInt64LittleEndian(
    view,
    MAGIC.length + 4,
    "Chunk plaintext length",
  );
  const keyId = view.getInt32(MAGIC.length + 4 + 8, true);
  const tagStart = MAGIC.length + 4 + 8 + 4;
  const header: ChunkHeader = {
    keyId,
    plaintextLength,
    tag: bytes.slice(tagStart, tagStart + GCM_TAG_BYTES),
  };

  assertParsedChunkHeader(header);
  return header;
}

export function buildChunkAad(
  keyId: number,
  chunkIndex: number,
  plaintextLength: number,
): Uint8Array {
  assertKeyId(keyId);
  assertChunkIndex(chunkIndex);
  assertPlaintextLength(plaintextLength);

  const output = new Uint8Array(32);
  const view = new DataView(output.buffer);

  output.set(MAGIC, 0);
  view.setInt32(4, CONTAINER_VERSION, true);
  view.setInt32(8, keyId, true);
  setInt64LittleEndian(view, 12, chunkIndex);
  setInt64LittleEndian(view, 20, plaintextLength);
  view.setInt32(28, 0, true);

  return output;
}

export function chunkNonce(noncePrefix: Uint8Array, chunkIndex: number): Uint8Array {
  assertByteLength(noncePrefix, GCM_NONCE_PREFIX_BYTES, "Nonce prefix");
  assertChunkIndex(chunkIndex);

  const nonce = new Uint8Array(GCM_NONCE_BYTES);
  const view = new DataView(nonce.buffer);
  nonce.set(noncePrefix, 0);
  view.setBigUint64(GCM_NONCE_PREFIX_BYTES, BigInt(chunkIndex), true);
  return nonce;
}

export function chunkPlaintextLength(
  chunkIndex: number,
  chunkSize: number,
  plaintextSize: number,
): number {
  assertChunkIndex(chunkIndex);
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

export function assertCompatibleChunkSize(chunkSize: number): void {
  if (
    !Number.isSafeInteger(chunkSize) ||
    chunkSize < MIN_CHUNK_SIZE ||
    chunkSize > MAX_CHUNK_SIZE
  ) {
    throw new InvalidCryptoInputError("Chunk size is outside the supported range.");
  }
}

function assertSupportedHeader(header: ContainerHeader): void {
  assertKeyId(header.keyId);
  assertByteLength(header.noncePrefix, GCM_NONCE_PREFIX_BYTES, "Nonce prefix");
  assertByteLength(header.fileKeyNonce, GCM_NONCE_BYTES, "File key nonce");
  assertByteLength(header.fileKeyTag, GCM_TAG_BYTES, "File key tag");
  assertByteLength(header.encryptedFileKey, FILE_KEY_BYTES, "Encrypted file key");
  assertPlaintextSize(header.plaintextSize);
}

function assertParsedHeader(header: ContainerHeader): void {
  try {
    assertSupportedHeader(header);
  } catch (error) {
    if (error instanceof InvalidCryptoInputError) {
      throw new CorruptedContainerError("Invalid file header contents.");
    }

    throw error;
  }
}

function assertParsedChunkHeader(header: ChunkHeader): void {
  try {
    assertKeyId(header.keyId);
    assertPlaintextLength(header.plaintextLength);
  } catch (error) {
    if (error instanceof InvalidCryptoInputError) {
      throw new CorruptedContainerError("Invalid chunk header contents.");
    }

    throw error;
  }
}

function assertPlaintextShape(plaintextSize: number, chunkSize: number): void {
  assertPlaintextSize(plaintextSize);
  assertCompatibleChunkSize(chunkSize);
}

function assertPlaintextSize(plaintextSize: number): void {
  if (!Number.isSafeInteger(plaintextSize) || plaintextSize < 0) {
    throw new InvalidCryptoInputError("Invalid plaintext size.");
  }
}

function assertPlaintextLength(plaintextLength: number): void {
  if (
    !Number.isSafeInteger(plaintextLength) ||
    plaintextLength <= 0 ||
    plaintextLength > MAX_CHUNK_SIZE
  ) {
    throw new InvalidCryptoInputError("Invalid chunk plaintext length.");
  }
}

function assertChunkIndex(chunkIndex: number): void {
  if (!Number.isSafeInteger(chunkIndex) || chunkIndex < 0) {
    throw new InvalidCryptoInputError("Chunk index must be a safe non-negative integer.");
  }
}

function assertKeyId(keyId: number): void {
  if (!Number.isInteger(keyId) || keyId <= 0 || keyId > 0x7fffffff) {
    throw new InvalidCryptoInputError("Key id must be a positive 32-bit integer.");
  }
}

function assertByteLength(bytes: Uint8Array, expected: number, name: string): void {
  if (bytes.length !== expected) {
    throw new InvalidCryptoInputError(`${name} must be ${expected} bytes.`);
  }
}

function viewFor(bytes: Uint8Array): DataView {
  return new DataView(bytes.buffer, bytes.byteOffset, bytes.byteLength);
}

function setInt64LittleEndian(view: DataView, byteOffset: number, value: number): void {
  view.setBigInt64(byteOffset, BigInt(value), true);
}

function getSafeInt64LittleEndian(
  view: DataView,
  byteOffset: number,
  fieldName: string,
): number {
  const raw = view.getBigInt64(byteOffset, true);

  if (raw < 0n || raw > BigInt(Number.MAX_SAFE_INTEGER)) {
    throw new CorruptedContainerError(`${fieldName} exceeds JavaScript safe range.`);
  }

  return Number(raw);
}
