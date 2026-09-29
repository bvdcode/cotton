import { afterEach, expect, it, vi } from "vitest";
import { nodesApi } from "../../../shared/api/nodesApi";
import {
  encryptDisplayMeta,
  generateMasterKey,
  useVault,
} from "../../../shared/crypto";
import { createFile, createFolder } from "../../../test/fileFixtures";
import { ConflictAction } from "../../../shared/types/nameConflict";
import { lookupUploadNames } from "./lookupUploadNames";
import { resolveUploadConflicts } from "./uploadConflicts";

afterEach(() => {
  useVault.getState().lock();
  vi.restoreAllMocks();
});

it("finds an encrypted replacement on a later page without sending the clear name", async () => {
  useVault
    .getState()
    .unlock(await generateMasterKey(), { persistToSession: false });
  const en = await encryptDisplayMeta({
    name: "private.jpg",
    contentType: "image/jpeg",
  });
  const existing = createFile({
    id: "existing",
    name: "opaque-id",
    metadata: { en, isClientEncrypted: "true" },
  });
  const get = vi
    .spyOn(nodesApi, "getChildren")
    .mockImplementation(async (_, options) => ({
      content: {
        ...createFolder(),
        nodes: [],
        files:
          options?.page === 1
            ? [createFile({ name: "other.txt" })]
            : [existing],
      },
      totalCount: 2,
    }));
  const lookup = vi.spyOn(nodesApi, "lookupSiblingNames");
  const files = [new File(["replacement"], "private.jpg")];
  const matches = await lookupUploadNames(
    "folder-1",
    files.map((file) => file.name),
    true,
  );
  const prompt = vi.fn(async () => ConflictAction.Overwrite);
  const result = await resolveUploadConflicts(files, matches, prompt);
  expect(result.files[0].replaceNodeFileId).toBe("existing");
  expect(prompt).toHaveBeenCalledWith(
    expect.objectContaining({ canOverwrite: true }),
  );
  expect(get).toHaveBeenCalledTimes(2);
  expect(lookup).not.toHaveBeenCalled();
});

it("rejects an encrypted lookup while locked", async () => {
  const get = vi.spyOn(nodesApi, "getChildren");
  await expect(
    lookupUploadNames("folder-1", ["private"], true),
  ).rejects.toThrow();
  expect(get).not.toHaveBeenCalled();
});
