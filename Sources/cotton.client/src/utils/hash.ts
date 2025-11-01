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
    return await new Promise<ArrayBuffer>((resolve, reject) => {
      const fr = new FileReader();
      fr.onerror = () => {
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

/**
 * Compute a deterministic overall hash from chunk hashes without re-reading the file.
 * Definition: digest( concat( digest(chunk_0), digest(chunk_1), ... ) )
 * where each digest(chunk_i) is provided as hex in `hashesHex`.
 */
export async function hashFromChunkHashes(
  hashesHex: string[],
  algorithm: AlgorithmIdentifier,
): Promise<string> {
  const bytes = concatHexDigests(hashesHex);
  // Ensure we pass an ArrayBuffer (not SharedArrayBuffer) to subtle.digest by copying into a fresh Uint8Array
  const copy = new Uint8Array(bytes.length);
  copy.set(bytes);
  const digest = await crypto.subtle.digest(algorithm, copy);
  return bufferToHex(digest);
}

function concatHexDigests(hexList: string[]): Uint8Array {
  const parts = hexList.map(hexToBytes);
  const total = parts.reduce((s, p) => s + p.length, 0);
  const out = new Uint8Array(total);
  let offset = 0;
  for (const p of parts) {
    out.set(p, offset);
    offset += p.length;
  }
  return out;
}

function hexToBytes(hex: string): Uint8Array {
  const clean = hex.length % 2 === 0 ? hex : `0${hex}`;
  const len = clean.length / 2;
  const out = new Uint8Array(len);
  for (let i = 0; i < len; i++) {
    const byte = clean.substr(i * 2, 2);
    out[i] = parseInt(byte, 16);
  }
  return out;
}

// =======================
// Incremental Hashers (no deps)
// =======================

class Sha256Hasher {
  private h0 = 0x6a09e667 | 0;
  private h1 = 0xbb67ae85 | 0;
  private h2 = 0x3c6ef372 | 0;
  private h3 = 0xa54ff53a | 0;
  private h4 = 0x510e527f | 0;
  private h5 = 0x9b05688c | 0;
  private h6 = 0x1f83d9ab | 0;
  private h7 = 0x5be0cd19 | 0;
  private buffer = new Uint8Array(64);
  private bufferLength = 0;
  private bytesLo = 0; // bit length low 32
  private bytesHi = 0; // bit length high 32
  private readonly W = new Int32Array(64);

  update(data: Uint8Array): this {
    let pos = 0;
    const len = data.length;
    // update bit length (in bits)
    const bits = (len >>> 0) * 8;
    this.bytesLo = (this.bytesLo + bits) >>> 0;
    this.bytesHi = (this.bytesHi + ((len / 0x20000000) >>> 0)) >>> 0; // len >>> 29
    if (this.bytesLo < bits) this.bytesHi = (this.bytesHi + 1) >>> 0;

    while (pos < len) {
      const take = Math.min(64 - this.bufferLength, len - pos);
      this.buffer.set(data.subarray(pos, pos + take), this.bufferLength);
      this.bufferLength += take;
      pos += take;
      if (this.bufferLength === 64) {
        this.processBlock(this.buffer);
        this.bufferLength = 0;
      }
    }
    return this;
  }

  private processBlock(chunk: Uint8Array) {
    const W = this.W;
    for (let i = 0; i < 16; i++) {
      const j = i * 4;
      W[i] =
        ((chunk[j] << 24) | (chunk[j + 1] << 16) | (chunk[j + 2] << 8) | chunk[j + 3]) | 0;
    }
    for (let i = 16; i < 64; i++) {
      const s0 = this.rotr(W[i - 15], 7) ^ this.rotr(W[i - 15], 18) ^ (W[i - 15] >>> 3);
      const s1 = this.rotr(W[i - 2], 17) ^ this.rotr(W[i - 2], 19) ^ (W[i - 2] >>> 10);
      W[i] = (((W[i - 16] + s0) | 0) + ((W[i - 7] + s1) | 0)) | 0;
    }

    let a = this.h0 | 0;
    let b = this.h1 | 0;
    let c = this.h2 | 0;
    let d = this.h3 | 0;
    let e = this.h4 | 0;
    let f = this.h5 | 0;
    let g = this.h6 | 0;
    let h = this.h7 | 0;

    for (let i = 0; i < 64; i++) {
      const S1 = this.rotr(e, 6) ^ this.rotr(e, 11) ^ this.rotr(e, 25);
      const ch = (e & f) ^ (~e & g);
      const temp1 = (h + S1 + ch + K[i] + W[i]) | 0;
      const S0 = this.rotr(a, 2) ^ this.rotr(a, 13) ^ this.rotr(a, 22);
      const maj = (a & b) ^ (a & c) ^ (b & c);
      const temp2 = (S0 + maj) | 0;

      h = g;
      g = f;
      f = e;
      e = (d + temp1) | 0;
      d = c;
      c = b;
      b = a;
      a = (temp1 + temp2) | 0;
    }

    this.h0 = (this.h0 + a) | 0;
    this.h1 = (this.h1 + b) | 0;
    this.h2 = (this.h2 + c) | 0;
    this.h3 = (this.h3 + d) | 0;
    this.h4 = (this.h4 + e) | 0;
    this.h5 = (this.h5 + f) | 0;
    this.h6 = (this.h6 + g) | 0;
    this.h7 = (this.h7 + h) | 0;
  }

  private rotr(x: number, n: number) {
    return (x >>> n) | (x << (32 - n));
  }

  digest(): Uint8Array {
    // padding
    this.buffer[this.bufferLength++] = 0x80;
    if (this.bufferLength > 56) {
      while (this.bufferLength < 64) this.buffer[this.bufferLength++] = 0;
      this.processBlock(this.buffer);
      this.bufferLength = 0;
    }
    while (this.bufferLength < 56) this.buffer[this.bufferLength++] = 0;
    // append 64-bit big-endian bit length
    const hi = this.bytesHi >>> 0;
    const lo = this.bytesLo >>> 0;
    this.buffer[56] = (hi >>> 24) & 0xff;
    this.buffer[57] = (hi >>> 16) & 0xff;
    this.buffer[58] = (hi >>> 8) & 0xff;
    this.buffer[59] = hi & 0xff;
    this.buffer[60] = (lo >>> 24) & 0xff;
    this.buffer[61] = (lo >>> 16) & 0xff;
    this.buffer[62] = (lo >>> 8) & 0xff;
    this.buffer[63] = lo & 0xff;
    this.processBlock(this.buffer);

    const out = new Uint8Array(32);
    const H = [
      this.h0 >>> 0,
      this.h1 >>> 0,
      this.h2 >>> 0,
      this.h3 >>> 0,
      this.h4 >>> 0,
      this.h5 >>> 0,
      this.h6 >>> 0,
      this.h7 >>> 0,
    ];
    for (let i = 0; i < 8; i++) {
      const v = H[i];
      const j = i * 4;
      out[j] = (v >>> 24) & 0xff;
      out[j + 1] = (v >>> 16) & 0xff;
      out[j + 2] = (v >>> 8) & 0xff;
      out[j + 3] = v & 0xff;
    }
    return out;
  }
}

const K = new Int32Array([
  0x428a2f98, 0x71374491, 0xb5c0fbcf, 0xe9b5dba5, 0x3956c25b, 0x59f111f1,
  0x923f82a4, 0xab1c5ed5, 0xd807aa98, 0x12835b01, 0x243185be, 0x550c7dc3,
  0x72be5d74, 0x80deb1fe, 0x9bdc06a7, 0xc19bf174, 0xe49b69c1, 0xefbe4786,
  0x0fc19dc6, 0x240ca1cc, 0x2de92c6f, 0x4a7484aa, 0x5cb0a9dc, 0x76f988da,
  0x983e5152, 0xa831c66d, 0xb00327c8, 0xbf597fc7, 0xc6e00bf3, 0xd5a79147,
  0x06ca6351, 0x14292967, 0x27b70a85, 0x2e1b2138, 0x4d2c6dfc, 0x53380d13,
  0x650a7354, 0x766a0abb, 0x81c2c92e, 0x92722c85, 0xa2bfe8a1, 0xa81a664b,
  0xc24b8b70, 0xc76c51a3, 0xd192e819, 0xd6990624, 0xf40e3585, 0x106aa070,
  0x19a4c116, 0x1e376c08, 0x2748774c, 0x34b0bcb5, 0x391c0cb3, 0x4ed8aa4a,
  0x5b9cca4f, 0x682e6ff3, 0x748f82ee, 0x78a5636f, 0x84c87814, 0x8cc70208,
  0x90befffa, 0xa4506ceb, 0xbef9a3f7, 0xc67178f2,
]);

class Sha1Hasher {
  private h0 = 0x67452301 | 0;
  private h1 = 0xefcdab89 | 0;
  private h2 = 0x98badcfe | 0;
  private h3 = 0x10325476 | 0;
  private h4 = 0xc3d2e1f0 | 0;
  private buffer = new Uint8Array(64);
  private bufferLength = 0;
  private bytesLo = 0; // bit length low 32
  private bytesHi = 0; // bit length high 32
  private readonly W = new Int32Array(80);

  update(data: Uint8Array): this {
    let pos = 0;
    const len = data.length;
    // update bit length
    const bits = (len >>> 0) * 8;
    this.bytesLo = (this.bytesLo + bits) >>> 0;
    this.bytesHi = (this.bytesHi + ((len / 0x20000000) >>> 0)) >>> 0;
    if (this.bytesLo < bits) this.bytesHi = (this.bytesHi + 1) >>> 0;

    while (pos < len) {
      const take = Math.min(64 - this.bufferLength, len - pos);
      this.buffer.set(data.subarray(pos, pos + take), this.bufferLength);
      this.bufferLength += take;
      pos += take;
      if (this.bufferLength === 64) {
        this.processBlock(this.buffer);
        this.bufferLength = 0;
      }
    }
    return this;
  }

  private rotl(x: number, n: number) {
    return (x << n) | (x >>> (32 - n));
  }

  private processBlock(chunk: Uint8Array) {
    const W = this.W;
    for (let i = 0; i < 16; i++) {
      const j = i * 4;
      W[i] =
        ((chunk[j] << 24) | (chunk[j + 1] << 16) | (chunk[j + 2] << 8) | chunk[j + 3]) | 0;
    }
    for (let i = 16; i < 80; i++) {
      W[i] = this.rotl(W[i - 3] ^ W[i - 8] ^ W[i - 14] ^ W[i - 16], 1) | 0;
    }

    let a = this.h0 | 0;
    let b = this.h1 | 0;
    let c = this.h2 | 0;
    let d = this.h3 | 0;
    let e = this.h4 | 0;

    for (let i = 0; i < 80; i++) {
      let f: number, k: number;
      if (i < 20) {
        f = (b & c) | (~b & d);
        k = 0x5a827999;
      } else if (i < 40) {
        f = b ^ c ^ d;
        k = 0x6ed9eba1;
      } else if (i < 60) {
        f = (b & c) | (b & d) | (c & d);
        k = 0x8f1bbcdc;
      } else {
        f = b ^ c ^ d;
        k = 0xca62c1d6;
      }
      const temp = (this.rotl(a, 5) + f + e + k + W[i]) | 0;
      e = d;
      d = c;
      c = this.rotl(b, 30) | 0;
      b = a;
      a = temp;
    }

    this.h0 = (this.h0 + a) | 0;
    this.h1 = (this.h1 + b) | 0;
    this.h2 = (this.h2 + c) | 0;
    this.h3 = (this.h3 + d) | 0;
    this.h4 = (this.h4 + e) | 0;
  }

  digest(): Uint8Array {
    this.buffer[this.bufferLength++] = 0x80;
    if (this.bufferLength > 56) {
      while (this.bufferLength < 64) this.buffer[this.bufferLength++] = 0;
      this.processBlock(this.buffer);
      this.bufferLength = 0;
    }
    while (this.bufferLength < 56) this.buffer[this.bufferLength++] = 0;
    const hi = this.bytesHi >>> 0;
    const lo = this.bytesLo >>> 0;
    this.buffer[56] = (hi >>> 24) & 0xff;
    this.buffer[57] = (hi >>> 16) & 0xff;
    this.buffer[58] = (hi >>> 8) & 0xff;
    this.buffer[59] = hi & 0xff;
    this.buffer[60] = (lo >>> 24) & 0xff;
    this.buffer[61] = (lo >>> 16) & 0xff;
    this.buffer[62] = (lo >>> 8) & 0xff;
    this.buffer[63] = lo & 0xff;
    this.processBlock(this.buffer);
    const H = [this.h0 >>> 0, this.h1 >>> 0, this.h2 >>> 0, this.h3 >>> 0, this.h4 >>> 0];
    const out = new Uint8Array(20);
    for (let i = 0; i < 5; i++) {
      const v = H[i];
      const j = i * 4;
      out[j] = (v >>> 24) & 0xff;
      out[j + 1] = (v >>> 16) & 0xff;
      out[j + 2] = (v >>> 8) & 0xff;
      out[j + 3] = v & 0xff;
    }
    return out;
  }
}

/**
 * Incremental hashing of a Blob without loading it into memory.
 * Supports SHA-256 and SHA-1.
 */
export async function hashBlobIncremental(
  blob: Blob,
  algorithm: AlgorithmIdentifier,
  chunkSize = 4 * 1024 * 1024,
): Promise<string> {
  const algo = typeof algorithm === "string" ? algorithm.toUpperCase() : String(algorithm).toUpperCase();
  const hasher = algo === "SHA-256" || algo === "SHA256" ? new Sha256Hasher() : new Sha1Hasher();
  let offset = 0;
  while (offset < blob.size) {
    const end = Math.min(offset + chunkSize, blob.size);
    const part = blob.slice(offset, end);
    const buf = new Uint8Array(await readBlobArrayBuffer(part));
    hasher.update(buf);
    offset = end;
  }
  const digest = hasher.digest();
  // Ensure ArrayBuffer (not SharedArrayBuffer) by copying if needed
  const out = new Uint8Array(digest.length);
  out.set(digest);
  return bufferToHex(out.buffer);
}
