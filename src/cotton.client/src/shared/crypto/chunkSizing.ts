import { InvalidCryptoInputError } from "./errors";

export const MIN_CHUNK_SIZE = 8 * 1024;
export const MAX_CHUNK_SIZE = 64 * 1024 * 1024;

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

function assertPlaintextShape(plaintextSize: number, chunkSize: number): void {
  assertPlaintextSize(plaintextSize);
  assertCompatibleChunkSize(chunkSize);
}

export function assertPlaintextSize(plaintextSize: number): void {
  if (!Number.isSafeInteger(plaintextSize) || plaintextSize < 0) {
    throw new InvalidCryptoInputError("Invalid plaintext size.");
  }
}

export function assertChunkIndex(chunkIndex: number): void {
  if (!Number.isSafeInteger(chunkIndex) || chunkIndex < 0) {
    throw new InvalidCryptoInputError(
      "Chunk index must be a safe non-negative integer.",
    );
  }
}
