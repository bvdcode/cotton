import { queryClient } from "../api/queries/queryClient";
import { queryKeys } from "../api/queries/queryKeys";
import { invalidateFolderInfoCache } from "../api/queries/folderInfo";

export const refreshNodeContent = async (nodeId: string): Promise<void> => {
  invalidateFolderInfoCache(queryClient);
  await queryClient.invalidateQueries({
    queryKey: queryKeys.nodeChildren.all(nodeId),
    predicate: ({ queryKey }) =>
      queryKey[3] !== "folderInfo" && queryKey[3] !== "folderSizes",
  });
};
