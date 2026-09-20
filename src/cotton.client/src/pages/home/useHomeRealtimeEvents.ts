import { useQueryClient } from "@tanstack/react-query";
import { useCallback, useEffect } from "react";
import type { NodeFileManifestDto } from "../../shared/api/nodesApi";
import { invalidateLayoutOverview } from "../../shared/api/queries/layouts";
import { queryKeys } from "../../shared/api/queries/queryKeys";
import {
  subscribeToPreviewGenerated,
  useFileTreeRealtimeInvalidation,
} from "../../shared/signalr";

export const useHomeRealtimeEvents = (
  enabled: boolean,
  layoutId: string | undefined,
): void => {
  const queryClient = useQueryClient();
  const handleInvalidate = useCallback((): void => {
    if (layoutId) {
      void invalidateLayoutOverview(queryClient, layoutId);
    }
  }, [layoutId, queryClient]);

  useFileTreeRealtimeInvalidation({
    enabled: enabled && Boolean(layoutId),
    onInvalidate: handleInvalidate,
  });

  useEffect(() => {
    if (!enabled || !layoutId) {
      return;
    }

    return subscribeToPreviewGenerated(
      (nodeId, nodeFileId, previewHashEncryptedHex) => {
        queryClient.setQueriesData<NodeFileManifestDto[]>(
          { queryKey: queryKeys.layouts.recentAll(layoutId) },
          (files) =>
            files?.map((file) =>
              file.id === nodeFileId && file.nodeId === nodeId
                ? { ...file, previewHashEncryptedHex }
                : file,
            ),
        );
      },
    );
  }, [enabled, layoutId, queryClient]);
};
