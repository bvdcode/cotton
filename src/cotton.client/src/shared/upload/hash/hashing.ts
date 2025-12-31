import { createSHA1, createSHA256, createSHA384, createSHA512 } from 'hash-wasm';

export type SupportedHashAlgorithm = "SHA-1" | "SHA-256" | "SHA-384" | "SHA-512";

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
  // Create hasher based on algorithm
  const hasher = await (async () => {
    switch (algorithm) {
      case "SHA-1": return createSHA1();
      case "SHA-256": return createSHA256();
      case "SHA-384": return createSHA384();
      case "SHA-512": return createSHA512();
      default: return createSHA256();
    }
  })();

  hasher.init();

  const CHUNK_SIZE = 64 * 1024 * 1024; // 64MB chunks
  let offset = 0;

  // Process file in chunks
  while (offset < blob.size) {
    const end = Math.min(offset + CHUNK_SIZE, blob.size);
    const chunkBlob = blob.slice(offset, end);
    const buffer = await chunkBlob.arrayBuffer();
    
    // Update hash incrementally - only this chunk is in memory!
    hasher.update(new Uint8Array(buffer));
    
    offset = end;

    // Yield to event loop to keep UI responsive
    if (offset < blob.size) {
      await new Promise(resolve => setTimeout(resolve, 0));
    }
  }

  // Finalize and return hex digest
  return hasher.digest('hex');
}
