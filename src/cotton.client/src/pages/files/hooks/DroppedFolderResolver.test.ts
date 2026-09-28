import { afterEach, expect, it, vi } from "vitest";
import { createFolder } from "../../../test/fileFixtures";
import { nodesApi } from "../../../shared/api/nodesApi";
import { DroppedFolderResolver } from "./DroppedFolderResolver";

afterEach(() => vi.restoreAllMocks());

it("merges dropped folders by name key and preserves the existing name", async () => {
  const parent = createFolder({ id: "parent" });
  const existing = createFolder({
    id: "existing",
    name: "Photos",
    parentId: parent.id,
  });
  const get = vi
    .spyOn(nodesApi, "getChildren")
    .mockResolvedValueOnce({
      content: {
        ...parent,
        nodes: Array.from({ length: 1000 }, (_, index) =>
          createFolder({ id: `folder-${index}`, name: `Folder ${index}` }),
        ),
        files: [],
      },
      totalCount: 1001,
    })
    .mockResolvedValueOnce({
      content: { ...parent, nodes: [existing], files: [] },
      totalCount: 1001,
    });
  const create = vi.spyOn(nodesApi, "createNode");
  const file = new File(["photo"], "photo.jpg");
  const secondFile = new File(["other"], "other.jpg");
  const resolver = new DroppedFolderResolver(
    parent.id,
    "Files",
    false,
    () => false,
  );

  const grouped = await resolver.groupFiles(
    [
      { file, relativePath: "photos/photo.jpg" },
      { file: secondFile, relativePath: "PHOTOS/other.jpg" },
    ],
    () => {},
  );

  expect(grouped.get(existing.id)).toEqual({
    label: "Files / Photos",
    files: [file, secondFile],
  });
  expect(get).toHaveBeenCalledTimes(2);
  expect(create).not.toHaveBeenCalled();
});
