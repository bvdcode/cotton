import { createSHA1, createSHA256, createSHA384, createSHA512 } from "hash-wasm";
import type { SupportedHashAlgorithm } from "./hashing";

type HashWasmHasher = {
  init(): void;
  update(data: Uint8Array): void;
  digest(encoding: "hex"): string;
};

type InitMessage = { type: "init"; requestId: string; algorithm: SupportedHashAlgorithm };

type HashChunkMessage = {
  type: "hashChunk";
  requestId: string;
  buffer: ArrayBuffer;
};

type DigestFileMessage = { type: "digestFile"; requestId: string };

type InMessage = InitMessage | HashChunkMessage | DigestFileMessage;

type InitResult = { type: "initResult"; requestId: string };

type HashChunkResult = { type: "hashChunkResult"; requestId: string; chunkHash: string };

type DigestFileResult = { type: "digestFileResult"; requestId: string; fileHash: string };

type ErrorResult = { type: "error"; requestId?: string; message: string };

type OutMessage = InitResult | HashChunkResult | DigestFileResult | ErrorResult;

let initialized = false;
let currentAlgorithm: SupportedHashAlgorithm | null = null;
let fileHasher: HashWasmHasher | null = null;
let chunkHasher: HashWasmHasher | null = null;

async function createHasher(algorithm: SupportedHashAlgorithm): Promise<HashWasmHasher> {
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

  const h = hasher as unknown as HashWasmHasher;
  h.init();
  return h;
}

async function ensureInitialized(algorithm: SupportedHashAlgorithm): Promise<void> {
  // IMPORTANT: hash-wasm requires calling init() before update(). After digest(),
  // the hasher is finalized and must be re-initialized for the next file.
  // Since we reuse workers across uploads, we must reset state on every init request.
  if (initialized && currentAlgorithm === algorithm && fileHasher && chunkHasher) {
    fileHasher.init();
    chunkHasher.init();
    return;
  }

  currentAlgorithm = algorithm;
  fileHasher = await createHasher(algorithm);
  chunkHasher = await createHasher(algorithm);
  initialized = true;
}

self.onmessage = async (ev: MessageEvent<InMessage>) => {
  const msg = ev.data;

  try {
    if (msg.type === "init") {
      await ensureInitialized(msg.algorithm);
      const out: OutMessage = { type: "initResult", requestId: msg.requestId };
      self.postMessage(out);
      return;
    }

    if (msg.type === "hashChunk") {
      if (!initialized || !fileHasher || !chunkHasher || !currentAlgorithm) {
        const out: OutMessage = { type: "error", requestId: msg.requestId, message: "Hasher is not initialized" };
        self.postMessage(out);
        return;
      }

      const bytes = new Uint8Array(msg.buffer);

      // Update whole-file hash incrementally.
      fileHasher.update(bytes);

      // Compute hash for this chunk.
      chunkHasher.init();
      chunkHasher.update(bytes);
      const chunkHash = chunkHasher.digest("hex");

      const out: OutMessage = { type: "hashChunkResult", requestId: msg.requestId, chunkHash };
      self.postMessage(out);
      return;
    }

    if (msg.type === "digestFile") {
      if (!initialized || !fileHasher) {
        const out: OutMessage = { type: "error", requestId: msg.requestId, message: "Hasher is not initialized" };
        self.postMessage(out);
        return;
      }

      const fileHash = fileHasher.digest("hex");
      // Reset so the worker can be reused even if the next consumer forgets to call init.
      fileHasher.init();
      const out: OutMessage = { type: "digestFileResult", requestId: msg.requestId, fileHash };
      self.postMessage(out);
      return;
    }
  } catch (e) {
    const message = e instanceof Error ? e.message : "Worker hashing failed";
    const requestId = "requestId" in msg ? msg.requestId : undefined;
    const out: OutMessage = { type: "error", requestId, message };
    self.postMessage(out);
  }
};
