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

const toHex = (buffer: ArrayBuffer): string => {
  const bytes = new Uint8Array(buffer);
  let out = "";
  for (const b of bytes) out += b.toString(16).padStart(2, "0");
  return out;
};

export async function hashBlob(blob: Blob, algorithm: SupportedHashAlgorithm): Promise<string> {
  const buffer = await blob.arrayBuffer();
  const digest = await crypto.subtle.digest(algorithm, buffer);
  return toHex(digest);
}

export async function hashFile(file: File, algorithm: SupportedHashAlgorithm): Promise<string> {
  // NOTE: This reads the whole file into memory.
  // If we need streaming hashes for very large files, we can swap this implementation.
  const buffer = await file.arrayBuffer();
  const digest = await crypto.subtle.digest(algorithm, buffer);
  return toHex(digest);
}
