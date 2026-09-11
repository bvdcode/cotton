import { describe, expect, it } from "vitest";
import {
  MAX_PINNED_FOLDERS,
  addPinnedFolder,
  parsePinnedFolderIds,
  removeMissingPinnedFolders,
  removePinnedFolder,
  serializePinnedFolderIds,
} from "./pinnedFolders";

const folderId = (index: number): string =>
  `00000000-0000-7000-8000-${index.toString().padStart(12, "0")}`;

describe("pinned folders", () => {
  it("parses unique folder ids in their saved order", () => {
    const first = folderId(1);
    const second = folderId(2);

    expect(
      parsePinnedFolderIds(JSON.stringify([first, second, first])),
    ).toEqual([first, second]);
  });

  it("rejects malformed preferences", () => {
    expect(parsePinnedFolderIds("not-json")).toEqual([]);
    expect(parsePinnedFolderIds(JSON.stringify(["not-a-guid"]))).toEqual([]);
  });

  it("never persists more than the configured limit", () => {
    const full = Array.from({ length: MAX_PINNED_FOLDERS }, (_, index) =>
      folderId(index + 1),
    );

    expect(addPinnedFolder(full, folderId(MAX_PINNED_FOLDERS + 1))).toEqual(
      full,
    );
    expect(
      parsePinnedFolderIds(
        JSON.stringify([...full, folderId(MAX_PINNED_FOLDERS + 1)]),
      ),
    ).toHaveLength(MAX_PINNED_FOLDERS);
  });

  it("adds, removes, and serializes folders", () => {
    const first = folderId(1);
    const second = folderId(2);
    const added = addPinnedFolder([first], second);

    expect(added).toEqual([first, second]);
    expect(addPinnedFolder(added, first)).toEqual(added);
    expect(removePinnedFolder(added, first)).toEqual([second]);
    expect(parsePinnedFolderIds(serializePinnedFolderIds(added))).toEqual(
      added,
    );
  });

  it("removes folder ids that could not be resolved", () => {
    const first = folderId(1);
    const missing = folderId(2);
    const third = folderId(3);

    expect(
      removeMissingPinnedFolders([first, missing, third], [third, first]),
    ).toEqual([first, third]);
  });
});
