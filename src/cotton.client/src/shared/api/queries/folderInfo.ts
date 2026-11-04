import {
  queryOptions,
  useQuery,
  type QueryClient,
  type QueryKey,
} from "@tanstack/react-query";
import { nodesApi } from "../nodesApi";
import { queryKeys } from "./queryKeys";
import { queryClient } from "./queryClient";
import { useAuthStore } from "../../store/authStore";

export const FOLDER_SIZE_STALE_TIME = 10 * 60_000;

export const folderInfoOptions = (
  nodeId: string,
  userId: string,
  calculationSignal?: AbortSignal,
) =>
  queryOptions({
    queryKey: queryKeys.nodeChildren.folderInfo(nodeId, userId),
    queryFn: ({ signal }) =>
      nodesApi.getFolderStats(
        nodeId,
        true,
        calculationSignal
          ? AbortSignal.any([signal, calculationSignal])
          : signal,
      ),
    gcTime: Infinity,
    staleTime: FOLDER_SIZE_STALE_TIME,
    retry: false,
  });

export const useCachedFolderSize = (nodeId: string) => {
  const userId = useAuthStore((state) => state.user?.id ?? "");
  return useQuery(
    {
      ...folderInfoOptions(nodeId, userId),
      enabled: false,
      select: (data) => data.sizeBytes,
    },
    queryClient,
  ).data;
};

const folderSizeQueries = {
  predicate: ({ queryKey }: { queryKey: QueryKey }) =>
    queryKey[0] === "nodeChildren" &&
    (queryKey[3] === "folderInfo" || queryKey[3] === "folderSizes"),
};

export const clearFolderInfoCache = (client: QueryClient) => {
  client.removeQueries(folderSizeQueries);
};

export const invalidateFolderInfoCache = async (client: QueryClient) => {
  await client.cancelQueries(folderSizeQueries);
  void client.invalidateQueries(folderSizeQueries);
};
