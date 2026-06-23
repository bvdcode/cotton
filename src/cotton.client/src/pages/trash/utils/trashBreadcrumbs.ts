import type { NodeDto } from "../../../shared/api/layoutsApi";

const trashWrapperNamePrefix = "trash-item-";
const originalParentPathMetadataKey = "originalParentPath";

const hasTrashWrapperName = (
  node: Pick<NodeDto, "name"> | null | undefined,
): boolean => node?.name.startsWith(trashWrapperNamePrefix) === true;

const hasOriginalParentPath = (
  node: Pick<NodeDto, "metadata"> | null | undefined,
): boolean => Boolean(node?.metadata?.[originalParentPathMetadataKey]);

export const isTrashWrapperNode = (
  node: Pick<NodeDto, "name" | "parentId" | "metadata"> | null | undefined,
  trashRootId: string | null | undefined,
): boolean => {
  if (!node || !trashRootId || !hasTrashWrapperName(node)) {
    return false;
  }

  return node.parentId === trashRootId && !hasOriginalParentPath(node);
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
  const wrapperIndex = chain.findIndex((node, index) => {
    if (!hasTrashWrapperName(node) || hasOriginalParentPath(node)) {
      return false;
    }

    return index === 0 || node.parentId === trashRootId;
  });

  return chain
    .filter((_, index) => index !== wrapperIndex)
    .map((node) => ({ id: node.id, name: node.name }));
};

export const isCurrentTrashWrapper = (
  ancestors: NodeDto[],
  currentNode: NodeDto | null,
): boolean => {
  if (
    !currentNode ||
    !hasTrashWrapperName(currentNode) ||
    hasOriginalParentPath(currentNode)
  ) {
    return false;
  }

  const trashRootId = ancestors.find((node) => !hasTrashWrapperName(node))?.id;
  return trashRootId
    ? currentNode.parentId === trashRootId
    : ancestors.length === 0;
};
