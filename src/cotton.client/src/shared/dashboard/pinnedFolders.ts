import { z } from "zod";

export const MAX_PINNED_FOLDERS = 128;

const pinnedFolderIdsSchema = z.array(z.uuid());

export const parsePinnedFolderIds = (value: string | undefined): string[] => {
  if (!value) {
    return [];
  }

  try {
    const parsed = pinnedFolderIdsSchema.safeParse(JSON.parse(value));
    if (!parsed.success) {
      return [];
    }

    return [...new Set(parsed.data)].slice(0, MAX_PINNED_FOLDERS);
  } catch {
    return [];
  }
};

export const serializePinnedFolderIds = (
  folderIds: readonly string[],
): string => JSON.stringify(folderIds.slice(0, MAX_PINNED_FOLDERS));

export const addPinnedFolder = (
  folderIds: readonly string[],
  folderId: string,
): string[] => {
  if (folderIds.includes(folderId) || folderIds.length >= MAX_PINNED_FOLDERS) {
    return [...folderIds];
  }

  return [...folderIds, folderId];
};

export const removePinnedFolder = (
  folderIds: readonly string[],
  folderId: string,
): string[] => folderIds.filter((candidate) => candidate !== folderId);

export const removeMissingPinnedFolders = (
  folderIds: readonly string[],
  resolvedFolderIds: readonly string[],
): string[] => {
  const resolvedFolderIdSet = new Set(resolvedFolderIds);
  return folderIds.filter((folderId) => resolvedFolderIdSet.has(folderId));
};
