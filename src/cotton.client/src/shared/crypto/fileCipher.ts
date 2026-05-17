import { decryptChunk, encryptChunk } from "./cipher";
import {
  ALG_AES_256_GCM,
  CONTAINER_VERSION,
  DEFAULT_CHUNK_SIZE,
  GCM_NONCE_PREFIX_BYTES,
  GCM_TAG_BYTES,
  buildHeader,
  chunkCount,
  chunkPlaintextLength,
  looksLikeContainer,
  parseHeader,
} from "./container";
import { CorruptedContainerError, NotAContainerError } from "./errors";
import {
  generateFileKey,
  randomBytes,
  unwrapFileKey,
  wrapFileKey,
} from "./keys";

export const ENCRYPTED_FLAG_KEY = "isClientEncrypted";
export const ORIGINAL_CONTENT_TYPE_KEY = "originalContentType";
export const ENCRYPTED_CONTENT_TYPE = "application/octet-stream";

const HEADER_PROBE_BYTES = 4096;

export async function encryptFileToBlob(
  plaintext: Blob,
  masterKey: CryptoKey,
  chunkSize: number = DEFAULT_CHUNK_SIZE,
): Promise<Blob> {
  const fileKey = await generateFileKey();
  const wrappedFileKey = await wrapFileKey(masterKey, fileKey);
  const noncePrefix = randomBytes(GCM_NONCE_PREFIX_BYTES);
  const header = buildHeader({
    containerVersion: CONTAINER_VERSION,
    contentAlgId: ALG_AES_256_GCM,
    chunkSize,
    noncePrefix,
    wrappedFileKey,
    plaintextSize: plaintext.size,
  });

  const parts: BlobPart[] = [asBlobPart(header)];
  const totalChunks = chunkCount(plaintext.size, chunkSize);

  for (let chunkIndex = 0; chunkIndex < totalChunks; chunkIndex += 1) {
    const chunkLength = chunkPlaintextLength(chunkIndex, chunkSize, plaintext.size);
    const chunkOffset = chunkIndex * chunkSize;
    const plaintextChunk = await readBlobSlice(
      plaintext,
      chunkOffset,
      chunkLength,
    );
    const ciphertextChunk = await encryptChunk(
      fileKey,
      noncePrefix,
      chunkIndex,
      plaintextChunk,
    );
    parts.push(asBlobPart(ciphertextChunk));
  }

  return new Blob(parts, { type: ENCRYPTED_CONTENT_TYPE });
}

export async function decryptBlobToBlob(
  encrypted: Blob,
  masterKey: CryptoKey,
  resultContentType: string = ENCRYPTED_CONTENT_TYPE,
): Promise<Blob> {
  const headerProbe = await readBlobSlice(
    encrypted,
    0,
    Math.min(HEADER_PROBE_BYTES, encrypted.size),
  );

  if (!looksLikeContainer(headerProbe)) {
    throw new NotAContainerError("Blob is not an encrypted Cotton container.");
  }

  const { header, headerLength } = parseHeader(headerProbe);
  const fileKey = await unwrapContainerFileKey(masterKey, header.wrappedFileKey);
  const totalChunks = chunkCount(header.plaintextSize, header.chunkSize);
  const parts: BlobPart[] = [];
  let cursor = headerLength;

  for (let chunkIndex = 0; chunkIndex < totalChunks; chunkIndex += 1) {
    const plaintextLength = chunkPlaintextLength(
      chunkIndex,
      header.chunkSize,
      header.plaintextSize,
    );
    const ciphertextLength = plaintextLength + GCM_TAG_BYTES;
    const ciphertextChunk = await readBlobSlice(
      encrypted,
      cursor,
      ciphertextLength,
    );

    if (ciphertextChunk.length !== ciphertextLength) {
      throw new CorruptedContainerError("Encrypted file is truncated.");
    }

    const plaintextChunk = await decryptChunk(
      fileKey,
      header.noncePrefix,
      chunkIndex,
      ciphertextChunk,
    );
    parts.push(asBlobPart(plaintextChunk));
    cursor += ciphertextLength;
  }

  if (cursor !== encrypted.size) {
    throw new CorruptedContainerError("Encrypted file has trailing bytes.");
  }

  return new Blob(parts, { type: resultContentType });
}

async function unwrapContainerFileKey(
  masterKey: CryptoKey,
  wrappedFileKey: Uint8Array,
): Promise<CryptoKey> {
  try {
    return await unwrapFileKey(masterKey, wrappedFileKey);
  } catch {
    throw new CorruptedContainerError("File key unwrap failed.");
  }
}

async function readBlobSlice(
  blob: Blob,
  offset: number,
  length: number,
): Promise<Uint8Array> {
  const buffer = await blob.slice(offset, offset + length).arrayBuffer();
  return new Uint8Array(buffer);
}

function asBlobPart(bytes: Uint8Array): BlobPart {
  return bytes as BlobPart;
}
