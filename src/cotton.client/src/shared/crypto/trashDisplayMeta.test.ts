import { afterEach, beforeEach, expect, it } from "vitest";
import { createFolder } from "../../test/fileFixtures";
import { generateMasterKey } from "./keys";
import { useVault } from "./vault";
import { encryptFolderName } from "./folderDisplayMeta";
import { projectTrashContent } from "./trashDisplayMeta";

const root = createFolder({ id: "root", parentId: null });
beforeEach(async () =>
  useVault
    .getState()
    .unlock(await generateMasterKey(), { persistToSession: false }),
);
afterEach(() => useVault.getState().lock());

it("projects saved trash paths without changing their raw snapshots", async () => {
  const parent = createFolder({
    name: "opaque-parent",
    metadata: { en: await encryptFolderName("Documents") },
  });
  const child = createFolder({
    name: "opaque-child",
    metadata: {
      en: await encryptFolderName("Archive"),
      originalParentPath: "opaque-parent",
      originalParentsV1: JSON.stringify([root, parent]),
    },
  });
  const source = { ...root, nodes: [child], files: [] };
  const display = await projectTrashContent(source);
  expect(display?.nodes[0].name).toBe("Archive");
  expect(display?.nodes[0].metadata?.originalParentPath).toBe("Documents");
  expect(source.nodes[0].metadata?.originalParentPath).toBe("opaque-parent");
  expect(JSON.stringify(source)).not.toContain("Documents");
});
