import {
  queryOptions,
  useQuery,
  type QueryClient,
} from "@tanstack/react-query";
import { nodesApi } from "../nodesApi";
import { queryKeys } from "./queryKeys";
import { queryClient } from "./queryClient";
import { useAuthStore } from "../../store/authStore";

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
    staleTime: Infinity,
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

export const clearFolderInfoCache = (client: QueryClient) => {
  client.removeQueries({
    predicate: ({ queryKey }) =>
      queryKey[0] === "nodeChildren" && queryKey[3] === "folderInfo",
  });
};
