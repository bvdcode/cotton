import { useEffect, useRef } from "react";
import {
  useMutation,
  useQueryClient,
  type QueryClient,
} from "@tanstack/react-query";
import { useTranslation } from "react-i18next";
import { nodesApi } from "@shared/api/nodesApi";
import { folderInfoOptions } from "@shared/api/queries/folderInfo";
import { useAuthStore } from "@shared/store/authStore";
import { toast } from "@shared/ui/notifications";
import { reportClientError } from "@shared/utils/clientDiagnostics";

const FOLDER_LIST_PAGE_SIZE = 10_000;

export const calculateFolderSizes = async (
  client: QueryClient,
  parentId: string,
  userId: string,
  signal: AbortSignal,
): Promise<void> => {
  let page = 1;
  const calculated = new Set<string>();
  while (!signal.aborted) {
    const { content, totalCount } = await nodesApi.getChildren(parentId, {
      page,
      pageSize: FOLDER_LIST_PAGE_SIZE,
      signal,
    });
    for (const folder of content.nodes) {
      signal.throwIfAborted();
      if (calculated.has(folder.id)) {
        continue;
      }
      await client.fetchQuery({
        ...folderInfoOptions(folder.id, userId, signal),
        staleTime: 0,
      });
      calculated.add(folder.id);
    }
    // The default directory listing returns folders before files.
    if (
      content.nodes.length < FOLDER_LIST_PAGE_SIZE ||
      page * FOLDER_LIST_PAGE_SIZE >= totalCount
    ) {
      return;
    }
    page += 1;
  }
};

export const useFolderSizes = (nodeId: string | null) => {
  const { t } = useTranslation("files");
  const client = useQueryClient();
  const userId = useAuthStore((state) => state.user?.id ?? null);
  const controller = useRef<AbortController | null>(null);
  useEffect(() => () => controller.current?.abort(), [nodeId, userId]);

  const calculation = useMutation({
    mutationFn: async () => {
      if (!nodeId || !userId) {
        return;
      }
      const run = new AbortController();
      controller.current = run;
      try {
        await calculateFolderSizes(client, nodeId, userId, run.signal);
      } catch (error) {
        if (run.signal.aborted) {
          return;
        }
        reportClientError("Failed to calculate folder sizes", error);
        throw error;
      }
    },
    onError: () => toast.error(t("folderSizes.failed")),
  });

  return {
    calculate: () => {
      if (!calculation.isPending) {
        calculation.mutate();
      }
    },
    isCalculating: calculation.isPending,
  };
};
