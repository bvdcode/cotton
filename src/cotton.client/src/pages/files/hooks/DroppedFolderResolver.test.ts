import { afterEach, expect, it, vi } from "vitest";
import { createFolder } from "../../../test/fileFixtures";
import { nodesApi } from "../../../shared/api/nodesApi";
import { DroppedFolderResolver } from "./DroppedFolderResolver";

afterEach(() => vi.restoreAllMocks());

it("reuses an existing dropped folder beyond the first page", async () => {
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
  const resolver = new DroppedFolderResolver(
    parent.id,
    "Files",
    false,
    () => false,
  );

  const grouped = await resolver.groupFiles(
    [{ file, relativePath: "Photos/photo.jpg" }],
    () => {},
  );

  expect(grouped.get(existing.id)?.files).toEqual([file]);
  expect(get).toHaveBeenCalledTimes(2);
  expect(create).not.toHaveBeenCalled();
});
