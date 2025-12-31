import { createSHA1, createSHA256, createSHA384, createSHA512 } from 'hash-wasm';

export type SupportedHashAlgorithm = "SHA-1" | "SHA-256" | "SHA-384" | "SHA-512";

type HashWasmHasher = {
  init(): void;
  update(data: Uint8Array): void;
  digest(encoding: 'hex'): string;
};

export type IncrementalHasher = {
  update(data: Uint8Array): void;
  digestHex(): string;
};

const normalize = (algorithm: string): string => algorithm.trim().toUpperCase();

export function toWebCryptoAlgorithm(serverAlgorithm: string): SupportedHashAlgorithm {
  const a = normalize(serverAlgorithm);

  // Accept common variants.
  if (a === "SHA256" || a === "SHA-256") return "SHA-256";
  if (a === "SHA1" || a === "SHA-1") return "SHA-1";
  if (a === "SHA384" || a === "SHA-384") return "SHA-384";
  if (a === "SHA512" || a === "SHA-512") return "SHA-512";

  // Safe default. If server advertises something else, we can extend later.
  return "SHA-256";
}

async function createHashWasmHasher(algorithm: SupportedHashAlgorithm): Promise<HashWasmHasher> {
  const hasher = await (async () => {
    switch (algorithm) {
      case "SHA-1":
        return createSHA1();
      case "SHA-256":
        return createSHA256();
      case "SHA-384":
        return createSHA384();
      case "SHA-512":
        return createSHA512();
      default:
        return createSHA256();
    }
  })();

  (hasher as unknown as HashWasmHasher).init();
  return hasher as unknown as HashWasmHasher;
}

export async function createIncrementalHasher(algorithm: SupportedHashAlgorithm): Promise<IncrementalHasher> {
  const hasher = await createHashWasmHasher(algorithm);
  return {
    update: (data) => hasher.update(data),
    digestHex: () => hasher.digest('hex'),
  };
}

export async function updateHasherFromBlob(blob: Blob, hasher: IncrementalHasher): Promise<void> {
  const anyBlob = blob as unknown as { stream?: () => ReadableStream<Uint8Array> };
  if (typeof anyBlob.stream === 'function') {
    const reader = anyBlob.stream().getReader();
    try {
      while (true) {
        const { done, value } = await reader.read();
        if (done) break;
        if (value && value.byteLength > 0) hasher.update(value);
      }
    } finally {
      reader.releaseLock();
    }
    return;
  }

  // Fallback for environments without Blob.stream().
  const buffer = await blob.arrayBuffer();
  hasher.update(new Uint8Array(buffer));
}

export async function hashBytes(bytes: Uint8Array, algorithm: SupportedHashAlgorithm): Promise<string> {
  const hasher = await createHashWasmHasher(algorithm);
  hasher.update(bytes);
  return hasher.digest('hex');
}

/**
 * Hash a blob using incremental hashing to avoid loading entire file into memory.
 * Uses hash-wasm for efficient streaming hash calculation.
 */
export async function hashBlob(blob: Blob, algorithm: SupportedHashAlgorithm): Promise<string> {
  return hashBlobStreaming(blob, algorithm);
}

/**
 * Hash a file using incremental hashing to avoid loading entire file into memory.
 * Critical for large files (multi-GB) - prevents OOM and UI freezes.
 */
export async function hashFile(file: File, algorithm: SupportedHashAlgorithm): Promise<string> {
  return hashBlobStreaming(file, algorithm);
}

/**
 * Incremental streaming hash using hash-wasm.
 * Reads file in chunks, updates hash incrementally without keeping entire file in memory.
 */
async function hashBlobStreaming(blob: Blob, algorithm: SupportedHashAlgorithm): Promise<string> {
  const hasher = await createIncrementalHasher(algorithm);
  await updateHasherFromBlob(blob, hasher);
  return hasher.digestHex();
}
