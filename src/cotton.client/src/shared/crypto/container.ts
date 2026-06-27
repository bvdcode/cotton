import {
  CorruptedContainerError,
  InvalidCryptoInputError,
  NotAContainerError,
} from "./errors";

/**
 * @deprecated OBSOLETE TRANSITION: CTN1 read support exists only until legacy client-side containers are gone.
 * Remove this legacy magic after the CTN2 transition cleanup.
 */
export const LEGACY_MAGIC = new Uint8Array([0x43, 0x54, 0x4e, 0x31]);
export const MAGIC = new Uint8Array([0x43, 0x54, 0x4e, 0x32]);
/**
 * @deprecated OBSOLETE TRANSITION: CTN1 read support exists only until legacy client-side containers are gone.
 * Remove this legacy version after the CTN2 transition cleanup.
 */
export const LEGACY_CONTAINER_VERSION = 1;
export const CONTAINER_VERSION = 2;
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

export type ContainerFormatVersion =
  | typeof LEGACY_CONTAINER_VERSION
  | typeof CONTAINER_VERSION;

export interface ContainerHeader {
  formatVersion: ContainerFormatVersion;
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
  return readFormatVersion(bytes) !== null;
}

export function requiresAuthenticatedTerminator(
  formatVersion: ContainerFormatVersion,
): boolean {
  return formatVersion >= CONTAINER_VERSION;
}

export function buildHeader(header: ContainerHeader): Uint8Array {
  assertSupportedHeader(header);

  const output = new Uint8Array(FILE_HEADER_BYTES);
  const view = new DataView(output.buffer);
  let offset = 0;

  output.set(magicForVersion(header.formatVersion), offset);
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
  const formatVersion = readFormatVersion(bytes);
  if (formatVersion === null) {
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
  const plaintextSize = getSafeInt64LittleEndian(
    view,
    offset,
    "Plaintext size",
  );
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
    formatVersion,
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

export function buildKeyAad(
  header: Pick<
    ContainerHeader,
    "formatVersion" | "keyId" | "noncePrefix" | "fileKeyNonce" | "plaintextSize"
  >,
): Uint8Array {
  assertFormatVersion(header.formatVersion);
  assertKeyId(header.keyId);
  assertByteLength(header.noncePrefix, GCM_NONCE_PREFIX_BYTES, "Nonce prefix");
  assertByteLength(header.fileKeyNonce, GCM_NONCE_BYTES, "File key nonce");
  assertPlaintextSize(header.plaintextSize);

  const output = new Uint8Array(
    MAGIC.length + 4 + 8 + 4 + GCM_NONCE_PREFIX_BYTES + GCM_NONCE_BYTES,
  );
  const view = new DataView(output.buffer);
  let offset = 0;

  output.set(magicForVersion(header.formatVersion), offset);
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

export function buildChunkHeader(
  header: ChunkHeader,
  formatVersion: ContainerFormatVersion = CONTAINER_VERSION,
): Uint8Array {
  assertFormatVersion(formatVersion);
  assertKeyId(header.keyId);
  assertPlaintextLength(header.plaintextLength, true);
  assertByteLength(header.tag, GCM_TAG_BYTES, "Chunk tag");

  const output = new Uint8Array(CHUNK_HEADER_BYTES);
  const view = new DataView(output.buffer);
  let offset = 0;

  output.set(magicForVersion(formatVersion), offset);
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

export function parseChunkHeader(
  bytes: Uint8Array,
  expectedFormatVersion?: ContainerFormatVersion,
): ChunkHeader {
  const actualFormatVersion = readFormatVersion(bytes);
  if (actualFormatVersion === null) {
    throw new CorruptedContainerError("Chunk header magic mismatch.");
  }

  if (
    expectedFormatVersion !== undefined &&
    actualFormatVersion !== expectedFormatVersion
  ) {
    throw new CorruptedContainerError("Chunk header format version mismatch.");
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
  formatVersion: ContainerFormatVersion = CONTAINER_VERSION,
): Uint8Array {
  assertFormatVersion(formatVersion);
  assertKeyId(keyId);
  assertChunkIndex(chunkIndex);
  assertPlaintextLength(plaintextLength, true);

  const output = new Uint8Array(32);
  const view = new DataView(output.buffer);

  output.set(magicForVersion(formatVersion), 0);
  view.setInt32(4, formatVersion, true);
  view.setInt32(8, keyId, true);
  setInt64LittleEndian(view, 12, chunkIndex);
  setInt64LittleEndian(view, 20, plaintextLength);
  view.setInt32(28, 0, true);

  return output;
}

export function chunkNonce(
  noncePrefix: Uint8Array,
  chunkIndex: number,
): Uint8Array {
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
    throw new InvalidCryptoInputError(
      "Chunk size is outside the supported range.",
    );
  }
}

function magicForVersion(formatVersion: ContainerFormatVersion): Uint8Array {
  if (formatVersion === CONTAINER_VERSION) {
    return MAGIC;
  }

  if (formatVersion === LEGACY_CONTAINER_VERSION) {
    return LEGACY_MAGIC;
  }

  throw new InvalidCryptoInputError("Unsupported container format version.");
}

function readFormatVersion(bytes: Uint8Array): ContainerFormatVersion | null {
  if (bytes.length < MAGIC.length) {
    return null;
  }

  if (startsWith(bytes, MAGIC)) {
    return CONTAINER_VERSION;
  }

  if (startsWith(bytes, LEGACY_MAGIC)) {
    return LEGACY_CONTAINER_VERSION;
  }

  return null;
}

function startsWith(bytes: Uint8Array, prefix: Uint8Array): boolean {
  for (let index = 0; index < prefix.length; index += 1) {
    if (bytes[index] !== prefix[index]) {
      return false;
    }
  }

  return true;
}

function assertSupportedHeader(header: ContainerHeader): void {
  assertFormatVersion(header.formatVersion);
  assertKeyId(header.keyId);
  assertByteLength(header.noncePrefix, GCM_NONCE_PREFIX_BYTES, "Nonce prefix");
  assertByteLength(header.fileKeyNonce, GCM_NONCE_BYTES, "File key nonce");
  assertByteLength(header.fileKeyTag, GCM_TAG_BYTES, "File key tag");
  assertByteLength(
    header.encryptedFileKey,
    FILE_KEY_BYTES,
    "Encrypted file key",
  );
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
    assertPlaintextLength(header.plaintextLength, true);
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

function assertPlaintextLength(
  plaintextLength: number,
  allowZero: boolean = false,
): void {
  const minimum = allowZero ? 0 : 1;
  if (
    !Number.isSafeInteger(plaintextLength) ||
    plaintextLength < minimum ||
    plaintextLength > MAX_CHUNK_SIZE
  ) {
    throw new InvalidCryptoInputError("Invalid chunk plaintext length.");
  }
}

function assertChunkIndex(chunkIndex: number): void {
  if (!Number.isSafeInteger(chunkIndex) || chunkIndex < 0) {
    throw new InvalidCryptoInputError(
      "Chunk index must be a safe non-negative integer.",
    );
  }
}

function assertKeyId(keyId: number): void {
  if (!Number.isInteger(keyId) || keyId <= 0 || keyId > 0x7fffffff) {
    throw new InvalidCryptoInputError(
      "Key id must be a positive 32-bit integer.",
    );
  }
}

function assertFormatVersion(
  formatVersion: number,
): asserts formatVersion is ContainerFormatVersion {
  if (
    formatVersion !== LEGACY_CONTAINER_VERSION &&
    formatVersion !== CONTAINER_VERSION
  ) {
    throw new InvalidCryptoInputError("Unsupported container format version.");
  }
}

function assertByteLength(
  bytes: Uint8Array,
  expected: number,
  name: string,
): void {
  if (bytes.length !== expected) {
    throw new InvalidCryptoInputError(`${name} must be ${expected} bytes.`);
  }
}

function viewFor(bytes: Uint8Array): DataView {
  return new DataView(bytes.buffer, bytes.byteOffset, bytes.byteLength);
}

function setInt64LittleEndian(
  view: DataView,
  byteOffset: number,
  value: number,
): void {
  view.setBigInt64(byteOffset, BigInt(value), true);
}

function getSafeInt64LittleEndian(
  view: DataView,
  byteOffset: number,
  fieldName: string,
): number {
  const raw = view.getBigInt64(byteOffset, true);

  if (raw < 0n || raw > BigInt(Number.MAX_SAFE_INTEGER)) {
    throw new CorruptedContainerError(
      `${fieldName} exceeds JavaScript safe range.`,
    );
  }

  return Number(raw);
}
