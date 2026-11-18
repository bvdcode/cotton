import { queryClient } from "../api/queries/queryClient";
import { refreshFolderListings } from "../api/queries/folderRefresh";
import { invalidateFolderInfoCache } from "../api/queries/folderInfo";

export const refreshNodeContent = async (nodeId: string): Promise<void> => {
  invalidateFolderInfoCache(queryClient);
  await refreshFolderListings(queryClient, nodeId);
};
