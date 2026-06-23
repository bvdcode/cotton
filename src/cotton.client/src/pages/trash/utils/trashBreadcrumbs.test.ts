import { describe, expect, it } from "vitest";
import type { NodeDto } from "../../../shared/api/layoutsApi";
import {
  buildVisibleTrashBreadcrumbs,
  isCurrentTrashWrapper,
  isTrashWrapperNode,
} from "./trashBreadcrumbs";

const makeNode = (
  id: string,
  name: string,
  parentId: string | null,
): NodeDto => ({
  id,
  name,
  parentId,
  layoutId: "layout-1",
  metadata: {},
  createdAt: "2026-05-18T00:00:00Z",
  updatedAt: "2026-05-18T00:00:00Z",
});

describe("trashBreadcrumbs", () => {
  const root = makeNode("trash-root", "Trash", null);
  const wrapper = makeNode("wrapper-1", "trash-item-vMkiHdSf", root.id);
  const deletedFolder = makeNode("deleted-1", "MSI4f78b.tmp", wrapper.id);
  const nestedFolder = makeNode("nested-1", "Screens", deletedFolder.id);

  it("hides the service wrapper between trash root and a deleted top-level folder", () => {
    expect(
      buildVisibleTrashBreadcrumbs([root, wrapper], deletedFolder),
    ).toEqual([
      { id: root.id, name: root.name },
      { id: deletedFolder.id, name: deletedFolder.name },
    ]);
  });

  it("keeps real descendants while hiding only the trash wrapper", () => {
    expect(
      buildVisibleTrashBreadcrumbs(
        [root, wrapper, deletedFolder],
        nestedFolder,
      ),
    ).toEqual([
      { id: root.id, name: root.name },
      { id: deletedFolder.id, name: deletedFolder.name },
      { id: nestedFolder.id, name: nestedFolder.name },
    ]);
  });

  it("detects wrapper routes so the page can redirect them back to trash root", () => {
    expect(isTrashWrapperNode(wrapper, root.id)).toBe(true);
    expect(isCurrentTrashWrapper([root], wrapper)).toBe(true);
    expect(isTrashWrapperNode(deletedFolder, root.id)).toBe(false);
  });
  it("hides the wrapper even when the trash root is missing from ancestors", () => {
    expect(buildVisibleTrashBreadcrumbs([wrapper], deletedFolder)).toEqual([
      { id: deletedFolder.id, name: deletedFolder.name },
    ]);
  });

  it("keeps deleted folders whose real name uses the trash wrapper prefix", () => {
    const realFolder = {
      ...makeNode("deleted-prefix", "trash-item-real", wrapper.id),
      metadata: { originalParentPath: "Home" },
    };

    expect(buildVisibleTrashBreadcrumbs([root, wrapper], realFolder)).toEqual([
      { id: root.id, name: root.name },
      { id: realFolder.id, name: realFolder.name },
    ]);
  });
});
