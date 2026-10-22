import { useEffect, useState } from "react";
import {
  useQuery,
  useQueryClient,
  type QueryClient,
} from "@tanstack/react-query";
import { useTranslation } from "react-i18next";
import { nodesApi, type NodeResponse } from "@shared/api/nodesApi";
import {
  FOLDER_SIZE_STALE_TIME,
  folderInfoOptions,
} from "@shared/api/queries/folderInfo";
import { FOLDER_PAGE_SIZE } from "@shared/api/queries/folderPageSize";
import { queryKeys } from "@shared/api/queries/queryKeys";
import { useAuthStore } from "@shared/store/authStore";
import { toast } from "@shared/ui/notifications";
import { reportClientError } from "@shared/utils/clientDiagnostics";

const getFolderPage = async (
  client: QueryClient,
  parentId: string,
  userId: string,
  page: number,
  signal: AbortSignal,
): Promise<NodeResponse> => {
  if (page === 1) {
    const overview = client.getQueryState<NodeResponse>(
      queryKeys.nodeChildren.overview(parentId, userId, FOLDER_PAGE_SIZE),
    );
    if (
      overview?.data &&
      !overview.isInvalidated &&
      Date.now() - overview.dataUpdatedAt < FOLDER_SIZE_STALE_TIME &&
      overview.fetchStatus === "idle"
    ) {
      return overview.data;
    }
  }
  return nodesApi.getChildren(parentId, {
    page,
    pageSize: FOLDER_PAGE_SIZE,
    signal,
  });
};

export const calculateFolderSizes = async (
  client: QueryClient,
  parentId: string,
  userId: string,
  signal: AbortSignal,
  onProgress: (sizeBytes: number) => void = () => {},
): Promise<number> => {
  let page = 1;
  let sizeBytes = 0;
  const calculated = new Set<string>();
  while (true) {
    signal.throwIfAborted();
    const { content, totalCount } = await getFolderPage(
      client,
      parentId,
      userId,
      page,
      signal,
    );
    for (const folder of content.nodes) {
      signal.throwIfAborted();
      if (calculated.has(folder.id)) {
        continue;
      }
      const stats = await client.fetchQuery(
        folderInfoOptions(folder.id, userId, signal),
      );
      signal.throwIfAborted();
      sizeBytes += stats.sizeBytes;
      onProgress(sizeBytes);
      calculated.add(folder.id);
    }
    // The default directory listing returns folders before files.
    if (
      content.nodes.length < FOLDER_PAGE_SIZE ||
      page * FOLDER_PAGE_SIZE >= totalCount
    ) {
      return sizeBytes;
    }
    page += 1;
  }
};

export const useFolderSizes = (nodeId: string | null, enabled = true) => {
  const { t } = useTranslation("files");
  const client = useQueryClient();
  const userId = useAuthStore((state) => state.user?.id ?? "");
  const [progress, setProgress] = useState({ nodeId, userId, sizeBytes: 0 });
  const calculation = useQuery({
    queryKey: queryKeys.nodeChildren.folderSizes(nodeId ?? "", userId),
    queryFn: ({ signal }) => {
      const onProgress = (sizeBytes: number) =>
        setProgress({ nodeId, userId, sizeBytes });
      onProgress(0);
      return calculateFolderSizes(client, nodeId!, userId, signal, onProgress);
    },
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

  const progressBytes =
    progress.nodeId === nodeId && progress.userId === userId
      ? progress.sizeBytes
      : 0;
  return {
    sizeBytes: enabled ? (calculation.data ?? progressBytes) : 0,
    isCalculating: calculation.isFetching,
  };
};
