import type { NodeDto } from "../../../shared/api/layoutsApi";

const trashWrapperNamePrefix = "trash-item-";

export const isTrashWrapperNode = (
  node: Pick<NodeDto, "name" | "parentId"> | null | undefined,
  trashRootId: string | null | undefined,
): boolean => {
  if (!node || !trashRootId) {
    return false;
  }

  return node.parentId === trashRootId && node.name.startsWith(trashWrapperNamePrefix);
};

export const buildVisibleTrashBreadcrumbs = (
  ancestors: NodeDto[],
  currentNode: NodeDto | null,
): Array<{ id: string; name: string }> => {
  if (!currentNode) {
    return [];
  }

  const chain = [...ancestors, currentNode];
  const trashRoot = chain[0] ?? null;
  const trashRootId = trashRoot?.id ?? null;

  return chain
    .filter((node) => !isTrashWrapperNode(node, trashRootId))
    .map((node) => ({ id: node.id, name: node.name }));
};

export const isCurrentTrashWrapper = (
  ancestors: NodeDto[],
  currentNode: NodeDto | null,
): boolean => isTrashWrapperNode(currentNode, ancestors[0]?.id ?? null);
