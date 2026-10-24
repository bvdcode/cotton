import { useEffect } from "react";
import {
  notifyManager,
  useQuery,
  useQueryClient,
  type QueryClient,
} from "@tanstack/react-query";
import { useTranslation } from "react-i18next";
import { nodesApi } from "@shared/api/nodesApi";
import { FOLDER_SIZE_STALE_TIME } from "@shared/api/queries/folderInfo";
import { queryKeys } from "@shared/api/queries/queryKeys";
import { useAuthStore } from "@shared/store/authStore";
import { toast } from "@shared/ui/notifications";
import { reportClientError } from "@shared/utils/clientDiagnostics";

interface FolderListingScope {
  nodeType: string;
  depth: number;
}

export const calculateFolderSizes = async (
  client: QueryClient,
  parentId: string,
  userId: string,
  signal: AbortSignal,
  scope?: FolderListingScope,
): Promise<number> => {
  signal.throwIfAborted();
  const folders = await nodesApi.getChildFolderStats(parentId, {
    ...scope,
    signal,
  });
  signal.throwIfAborted();
  let sizeBytes = 0;
  notifyManager.batch(() => {
    for (const [id, stats] of Object.entries(folders)) {
      client.setQueryData(queryKeys.nodeChildren.folderInfo(id, userId), stats);
      sizeBytes += stats.sizeBytes;
    }
  });
  return sizeBytes;
};

export const useFolderSizes = (
  nodeId: string | null,
  enabled = true,
  scope?: FolderListingScope,
) => {
  const { t } = useTranslation("files");
  const client = useQueryClient();
  const userId = useAuthStore((state) => state.user?.id ?? "");
  const calculation = useQuery({
    queryKey: scope
      ? [...queryKeys.nodeChildren.folderSizes(nodeId ?? "", userId), scope]
      : queryKeys.nodeChildren.folderSizes(nodeId ?? "", userId),
    queryFn: ({ signal }) =>
      calculateFolderSizes(client, nodeId!, userId, signal, scope),
    enabled: enabled && Boolean(nodeId && userId),
    staleTime: FOLDER_SIZE_STALE_TIME,
    gcTime: Infinity,
    retry: false,
  });

  useEffect(() => {
    if (calculation.error) {
      reportClientError("Failed to calculate folder sizes", calculation.error);
      toast.error(t("folderSizes.failed"));
    }
  }, [calculation.error, t]);

  return {
    sizeBytes: enabled ? (calculation.data ?? 0) : 0,
    isCalculating: calculation.isFetching,
  };
};
