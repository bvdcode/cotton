import { useNodesStore } from "../store/nodesStore";
import type {
  MoveClipboardItem,
  FileTransferOperation,
} from "../store/moveClipboardStore";
import type { MoveSingleItemResult } from "./moveConflictResolution";

export const applyTransferToCache = (
  item: MoveClipboardItem,
  moved: MoveSingleItemResult,
  targetParentId: string,
  operation: FileTransferOperation,
): MoveClipboardItem => {
  const store = useNodesStore.getState();
  if (moved.kind === "folder") {
    switch (operation) {
      case "move":
        store.moveFolderInCache(
          moved.folder,
          item.sourceParentId,
          targetParentId,
        );
        return item;
      case "copy":
        store.addFolderToCache(targetParentId, moved.folder);
        return { ...item, id: moved.folder.id, sourceParentId: targetParentId };
    }
  }
  switch (operation) {
    case "move":
      store.moveFileInCache(moved.file, item.sourceParentId, targetParentId);
      return { ...item, id: moved.file.id, file: moved.file };
    case "copy":
      store.upsertFileInCache(targetParentId, moved.file);
      return {
        ...item,
        id: moved.file.id,
        sourceParentId: targetParentId,
        file: moved.file,
      };
  }
};

export function removeMergedFolderFromCache(item: MoveClipboardItem): void {
  useNodesStore.setState((state) => {
    const contentByNodeId = { ...state.contentByNodeId };
    delete contentByNodeId[item.id];
    for (const [id, content] of Object.entries(contentByNodeId)) {
      if (content?.nodes.some((node) => node.id === item.id)) {
        contentByNodeId[id] = {
          ...content,
          nodes: content.nodes.filter((node) => node.id !== item.id),
        };
      }
    }
    return { contentByNodeId };
  });
}
