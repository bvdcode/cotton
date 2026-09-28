import { queryClient } from "../api/queries/queryClient";
import { queryKeys } from "../api/queries/queryKeys";

export const refreshNodeContent = async (nodeId: string): Promise<void> => {
  await queryClient.invalidateQueries({
    queryKey: queryKeys.nodeChildren.all(nodeId),
  });
};
