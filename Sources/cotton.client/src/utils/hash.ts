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
  const ab = await blob.arrayBuffer();
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
