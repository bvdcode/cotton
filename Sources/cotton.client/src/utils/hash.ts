import type { SupportedHashAlgorithm } from "../stores/settingsStore.ts";

export function normalizeAlgorithm(
  algo: SupportedHashAlgorithm,
): AlgorithmIdentifier {
  // Map possible aliases from server to Web Crypto names
  switch (algo.toUpperCase()) {
    case "SHA256":
      return "SHA-256";
    case "SHA-256":
      return "SHA-256";
    case "SHA1":
    case "SHA-1":
      return "SHA-1";
    case "MD5":
      // Web Crypto doesn't support MD5; throw explicit
      throw new Error("MD5 is not supported by SubtleCrypto");
    default:
      return algo;
  }
}

export async function hashBlob(
  blob: Blob,
  algorithm: AlgorithmIdentifier,
): Promise<string> {
  const ab = await readBlobArrayBuffer(blob);
  const digest = await crypto.subtle.digest(algorithm, ab);
  return bufferToHex(digest);
}

export function bufferToHex(buffer: ArrayBuffer): string {
  const bytes = new Uint8Array(buffer);
  const hex: string[] = [];
  for (let i = 0; i < bytes.length; i++) {
    const h = bytes[i].toString(16).padStart(2, "0");
    hex.push(h);
  }
  return hex.join("");
}

/**
 * Read a Blob into an ArrayBuffer with a fallback to FileReader for browsers/environments
 * that may intermittently throw NotReadableError on Blob.arrayBuffer().
 */
export async function readBlobArrayBuffer(blob: Blob): Promise<ArrayBuffer> {
  try {
    return await blob.arrayBuffer();
  } catch {
    // Fallback via FileReader to work around sporadic NotReadableError scenarios
    return await new Promise<ArrayBuffer>((resolve, reject) => {
      const fr = new FileReader();
      fr.onerror = () => {
        // Normalize the error type so callers can distinguish NotReadableError
        const err = fr.error ?? new DOMException("Failed to read blob", "NotReadableError");
        reject(err);
      };
      fr.onload = () => resolve(fr.result as ArrayBuffer);
      try {
        fr.readAsArrayBuffer(blob);
      } catch (err) {
        reject(err);
      }
    });
  }
}

export function isNotReadableError(err: unknown): boolean {
  return err instanceof DOMException && err.name === "NotReadableError";
}
