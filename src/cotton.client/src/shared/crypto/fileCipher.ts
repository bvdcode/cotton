import { decryptChunk, encryptChunk } from "./cipher";
import {
  CHUNK_HEADER_BYTES,
  CONTAINER_VERSION,
  DEFAULT_KEY_ID,
  DEFAULT_CHUNK_SIZE,
  FILE_HEADER_BYTES,
  GCM_NONCE_BYTES,
  GCM_NONCE_PREFIX_BYTES,
  assertCompatibleChunkSize,
  buildChunkAad,
  buildChunkHeader,
  buildHeader,
  buildKeyAad,
  chunkCount,
  chunkPlaintextLength,
  looksLikeContainer,
  parseChunkHeader,
  parseHeader,
  requiresAuthenticatedTerminator,
  type ContainerHeader,
} from "./container";
import { CorruptedContainerError, NotAContainerError } from "./errors";
import {
  generateFileKey,
  randomBytes,
  unwrapFileKey,
  wrapFileKey,
} from "./keys";
import { assertClientEncryptionBlobPipelineSize } from "./limits";

export const ENCRYPTED_FLAG_KEY = "isClientEncrypted";
export const ORIGINAL_CONTENT_TYPE_KEY = "originalContentType";
export const ENCRYPTED_CONTENT_TYPE = "application/octet-stream";

const HEADER_PROBE_BYTES = FILE_HEADER_BYTES;

export interface FileCipherProgressCallbacks {
  onProgress?: (bytesProcessed: number, bytesTotal: number) => void;
}

export async function encryptFileToBlob(
  plaintext: Blob,
  masterKey: CryptoKey,
  chunkSize: number = DEFAULT_CHUNK_SIZE,
  callbacks?: FileCipherProgressCallbacks,
): Promise<Blob> {
  assertClientEncryptionBlobPipelineSize(plaintext.size, "encrypt");
  assertCompatibleChunkSize(chunkSize);

  const fileKey = await generateFileKey();
  const noncePrefix = randomBytes(GCM_NONCE_PREFIX_BYTES);
  const fileKeyNonce = randomBytes(GCM_NONCE_BYTES);
  const keyHeader: Pick<
    ContainerHeader,
    "formatVersion" | "keyId" | "noncePrefix" | "fileKeyNonce" | "plaintextSize"
  > = {
    formatVersion: CONTAINER_VERSION,
    keyId: DEFAULT_KEY_ID,
    noncePrefix,
    fileKeyNonce,
    plaintextSize: plaintext.size,
  };
  const keyAad = buildKeyAad(keyHeader);
  const wrappedFileKey = await wrapFileKey(masterKey, fileKey, fileKeyNonce, keyAad);
  const header = buildHeader({
    ...keyHeader,
    fileKeyTag: wrappedFileKey.tag,
    encryptedFileKey: wrappedFileKey.encryptedFileKey,
  });

  const parts: BlobPart[] = [asBlobPart(header)];
  const totalChunks = chunkCount(plaintext.size, chunkSize);
  let bytesProcessed = 0;
  callbacks?.onProgress?.(0, plaintext.size);

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
      buildChunkAad(DEFAULT_KEY_ID, chunkIndex, chunkLength, keyHeader.formatVersion),
    );
    parts.push(
      asBlobPart(
        buildChunkHeader(
          {
            keyId: DEFAULT_KEY_ID,
            plaintextLength: chunkLength,
            tag: ciphertextChunk.tag,
          },
          keyHeader.formatVersion,
        ),
      ),
      asBlobPart(ciphertextChunk.ciphertext),
    );
    bytesProcessed += chunkLength;
    callbacks?.onProgress?.(bytesProcessed, plaintext.size);
  }

  const terminatorChunk = await encryptChunk(
    fileKey,
    noncePrefix,
    totalChunks,
    new Uint8Array(),
    buildChunkAad(DEFAULT_KEY_ID, totalChunks, 0, keyHeader.formatVersion),
  );
  parts.push(
    asBlobPart(
      buildChunkHeader(
        {
          keyId: DEFAULT_KEY_ID,
          plaintextLength: 0,
          tag: terminatorChunk.tag,
        },
        keyHeader.formatVersion,
      ),
    ),
  );

  return new Blob(parts, { type: ENCRYPTED_CONTENT_TYPE });
}

export async function decryptBlobToBlob(
  encrypted: Blob,
  masterKey: CryptoKey,
  resultContentType: string = ENCRYPTED_CONTENT_TYPE,
  callbacks?: FileCipherProgressCallbacks,
): Promise<Blob> {
  assertClientEncryptionBlobPipelineSize(encrypted.size, "decrypt");

  const headerProbe = await readBlobSlice(
    encrypted,
    0,
    Math.min(HEADER_PROBE_BYTES, encrypted.size),
  );

  if (!looksLikeContainer(headerProbe)) {
    throw new NotAContainerError("Blob is not an encrypted Cotton container.");
  }

  const { header, headerLength } = parseHeader(headerProbe);
  const fileKey = await unwrapContainerFileKey(masterKey, header);
  const parts: BlobPart[] = [];
  let cursor = headerLength;
  let remainingPlaintext = header.plaintextSize;
  let bytesProcessed = 0;
  let chunkIndex = 0;
  callbacks?.onProgress?.(0, header.plaintextSize);

  for (; remainingPlaintext > 0; chunkIndex += 1) {
    const chunkHeaderBytes = await readBlobSlice(
      encrypted,
      cursor,
      CHUNK_HEADER_BYTES,
    );

    if (chunkHeaderBytes.length !== CHUNK_HEADER_BYTES) {
      throw new CorruptedContainerError("Encrypted file is truncated.");
    }

    const chunkHeader = parseChunkHeader(chunkHeaderBytes, header.formatVersion);

    if (chunkHeader.keyId !== header.keyId) {
      throw new CorruptedContainerError("Chunk key id does not match file key id.");
    }

    if (chunkHeader.plaintextLength === 0) {
      throw new CorruptedContainerError("Data chunk cannot be an encrypted terminator.");
    }

    if (chunkHeader.plaintextLength > remainingPlaintext) {
      throw new CorruptedContainerError("Chunk plaintext length exceeds file length.");
    }

    cursor += CHUNK_HEADER_BYTES;
    const ciphertextChunk = await readBlobSlice(
      encrypted,
      cursor,
      chunkHeader.plaintextLength,
    );

    if (ciphertextChunk.length !== chunkHeader.plaintextLength) {
      throw new CorruptedContainerError("Encrypted file is truncated.");
    }

    const plaintextChunk = await decryptChunk(
      fileKey,
      header.noncePrefix,
      chunkIndex,
      ciphertextChunk,
      chunkHeader.tag,
      buildChunkAad(
        header.keyId,
        chunkIndex,
        chunkHeader.plaintextLength,
        header.formatVersion,
      ),
    );
    parts.push(asBlobPart(plaintextChunk));
    cursor += chunkHeader.plaintextLength;
    remainingPlaintext -= chunkHeader.plaintextLength;
    bytesProcessed += chunkHeader.plaintextLength;
    callbacks?.onProgress?.(bytesProcessed, header.plaintextSize);
  }

  cursor = await readAuthenticatedTerminatorIfPresent(
    encrypted,
    cursor,
    fileKey,
    header,
    chunkIndex,
  );

  if (cursor !== encrypted.size) {
    throw new CorruptedContainerError("Encrypted file has trailing bytes.");
  }

  return new Blob(parts, { type: resultContentType });
}

async function readAuthenticatedTerminatorIfPresent(
  encrypted: Blob,
  cursor: number,
  fileKey: CryptoKey,
  header: ContainerHeader,
  chunkIndex: number,
): Promise<number> {
  if (cursor === encrypted.size && !requiresAuthenticatedTerminator(header.formatVersion)) {
    return cursor;
  }

  const chunkHeaderBytes = await readBlobSlice(
    encrypted,
    cursor,
    CHUNK_HEADER_BYTES,
  );

  if (chunkHeaderBytes.length !== CHUNK_HEADER_BYTES) {
    throw new CorruptedContainerError("Encrypted file terminator is missing.");
  }

  const terminatorHeader = parseChunkHeader(
    chunkHeaderBytes,
    header.formatVersion,
  );
  if (terminatorHeader.keyId !== header.keyId) {
    throw new CorruptedContainerError("Terminator key id does not match file key id.");
  }

  if (terminatorHeader.plaintextLength !== 0) {
    throw new CorruptedContainerError("Encrypted file has trailing bytes.");
  }

  await decryptChunk(
    fileKey,
    header.noncePrefix,
    chunkIndex,
    new Uint8Array(),
    terminatorHeader.tag,
    buildChunkAad(header.keyId, chunkIndex, 0, header.formatVersion),
  );

  return cursor + CHUNK_HEADER_BYTES;
}

async function unwrapContainerFileKey(
  masterKey: CryptoKey,
  header: ContainerHeader,
): Promise<CryptoKey> {
  try {
    return await unwrapFileKey(
      masterKey,
      header.encryptedFileKey,
      header.fileKeyTag,
      header.fileKeyNonce,
      buildKeyAad(header),
    );
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
