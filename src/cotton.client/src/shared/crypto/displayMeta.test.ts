import { afterEach, beforeEach, describe, expect, it } from "vitest";
import type { NodeFileManifestDto } from "../api/nodesApi";
import {
  applyDisplayMetaToFile,
  applyDisplayMetaToFiles,
  decryptDisplayMeta,
  DISPLAY_META_KEY,
  encryptDisplayMeta,
  toPersistableFileDisplayMetadata,
} from "./displayMeta";
import { ENCRYPTED_FLAG_KEY } from "./fileCipher";
import { InvalidCryptoInputError } from "./errors";
import { generateMasterKey } from "./keys";
import { useVault } from "./vault";

function createFile(
  overrides: Partial<NodeFileManifestDto> = {},
): NodeFileManifestDto {
  return {
    id: "file-1",
    createdAt: "2026-01-01T00:00:00.000Z",
    updatedAt: "2026-01-01T00:00:00.000Z",
    nodeId: "node-1",
    ownerId: "owner-1",
    name: "server-name",
    contentType: "application/octet-stream",
    sizeBytes: 12,
    metadata: {},
    ...overrides,
  };
}

describe("display metadata encryption", () => {
  beforeEach(async () => {
    useVault.getState().unlock(await generateMasterKey());
  });

  afterEach(() => {
    useVault.getState().lock();
  });

  it("round-trips filename and content type", async () => {
    const encrypted = await encryptDisplayMeta({
      name: "photo.png",
      contentType: "image/png",
    });

    await expect(decryptDisplayMeta(encrypted)).resolves.toEqual({
      name: "photo.png",
      contentType: "image/png",
    });
  });

  it("uses a fresh nonce for repeated metadata encryption", async () => {
    const first = await encryptDisplayMeta({
      name: "same.txt",
      contentType: "text/plain",
    });
    const second = await encryptDisplayMeta({
      name: "same.txt",
      contentType: "text/plain",
    });

    expect(first).not.toBe(second);
  });

  it("rejects missing display fields before encrypting", async () => {
    await expect(
      encryptDisplayMeta({ name: " ", contentType: "text/plain" }),
    ).rejects.toBeInstanceOf(InvalidCryptoInputError);
    await expect(
      encryptDisplayMeta({ name: "file.txt", contentType: " " }),
    ).rejects.toBeInstanceOf(InvalidCryptoInputError);
  });

  it("rejects tampered metadata", async () => {
    const encrypted = await encryptDisplayMeta({
      name: "secret.pdf",
      contentType: "application/pdf",
    });
    const tampered = `${encrypted.slice(0, -2)}AA`;

    await expect(decryptDisplayMeta(tampered)).rejects.toBeDefined();
  });
});

describe("applyDisplayMetaToFile", () => {
  afterEach(() => {
    useVault.getState().lock();
  });

  it("leaves files unchanged while the vault is locked", async () => {
    const file = createFile({
      metadata: {
        [ENCRYPTED_FLAG_KEY]: "true",
        [DISPLAY_META_KEY]: "not-readable-while-locked",
      },
    });

    await expect(applyDisplayMetaToFile(file)).resolves.toBe(file);
  });

  it("leaves plain files unchanged", async () => {
    useVault.getState().unlock(await generateMasterKey());
    const file = createFile({
      name: "plain.txt",
      contentType: "text/plain",
    });

    await expect(applyDisplayMetaToFile(file)).resolves.toBe(file);
  });

  it("overlays encrypted display values without mutating the source file", async () => {
    useVault.getState().unlock(await generateMasterKey());
    const encrypted = await encryptDisplayMeta({
      name: "secret.pdf",
      contentType: "application/pdf",
    });
    const file = createFile({
      metadata: {
        [ENCRYPTED_FLAG_KEY]: "true",
        [DISPLAY_META_KEY]: encrypted,
      },
    });

    const decorated = await applyDisplayMetaToFile(file);

    expect(decorated).not.toBe(file);
    expect(decorated.name).toBe("secret.pdf");
    expect(decorated.contentType).toBe("application/pdf");
    expect(file.name).toBe("server-name");
    expect(file.contentType).toBe("application/octet-stream");
  });

  it("restores opaque values before persisting a decorated encrypted file", async () => {
    useVault.getState().unlock(await generateMasterKey());
    const encrypted = await encryptDisplayMeta({
      name: "secret.pdf",
      contentType: "application/pdf",
    });
    const file = createFile({
      name: "11111111-2222-4333-8444-555555555555",
      contentType: "application/octet-stream",
      metadata: {
        [ENCRYPTED_FLAG_KEY]: "true",
        [DISPLAY_META_KEY]: encrypted,
      },
    });

    const decorated = await applyDisplayMetaToFile(file);
    const persistable = toPersistableFileDisplayMetadata(decorated);

    expect(persistable).toMatchObject({
      name: "11111111-2222-4333-8444-555555555555",
      contentType: "application/octet-stream",
    });
  });

  it("preserves opaque values when a decorated file is decorated again", async () => {
    useVault.getState().unlock(await generateMasterKey());
    const encrypted = await encryptDisplayMeta({
      name: "secret.pdf",
      contentType: "application/pdf",
    });
    const file = createFile({
      name: "11111111-2222-4333-8444-555555555555",
      contentType: "application/octet-stream",
      metadata: {
        [ENCRYPTED_FLAG_KEY]: "true",
        [DISPLAY_META_KEY]: encrypted,
      },
    });

    const first = await applyDisplayMetaToFile(file);
    const second = await applyDisplayMetaToFile(first);

    expect(toPersistableFileDisplayMetadata(second)).toMatchObject({
      name: "11111111-2222-4333-8444-555555555555",
      contentType: "application/octet-stream",
    });
  });

  it("skips unlocked encrypted files when opaque values are unavailable", async () => {
    useVault.getState().unlock(await generateMasterKey());
    const file = createFile({
      name: "secret.pdf",
      contentType: "application/pdf",
      metadata: {
        [ENCRYPTED_FLAG_KEY]: "true",
      },
    });

    expect(toPersistableFileDisplayMetadata(file)).toBeNull();
  });

  it("falls back to the original object when display metadata is corrupted", async () => {
    useVault.getState().unlock(await generateMasterKey());
    const file = createFile({
      metadata: {
        [ENCRYPTED_FLAG_KEY]: "true",
        [DISPLAY_META_KEY]: "garbage",
      },
    });

    await expect(applyDisplayMetaToFile(file)).resolves.toBe(file);
  });

  it("returns the original array when no file changes", async () => {
    useVault.getState().unlock(await generateMasterKey());
    const files = [createFile({ name: "plain.txt" })];

    await expect(applyDisplayMetaToFiles(files)).resolves.toBe(files);
  });
});
