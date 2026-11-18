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

    return subscribeToPreviewGenerated((previews) => {
      queryClient.setQueriesData<NodeFileManifestDto[]>(
        { queryKey: queryKeys.layouts.recentAll(layoutId) },
        (files) => {
          let changed = false;
          const updated = files?.map((file) => {
            const previewHashEncryptedHex = previews
              .get(file.nodeId)
              ?.get(file.id);
            if (
              previewHashEncryptedHex === undefined ||
              file.previewHashEncryptedHex === previewHashEncryptedHex
            ) {
              return file;
            }
            changed = true;
            return { ...file, previewHashEncryptedHex };
          });
          return changed ? updated : files;
        },
      );
    });
  }, [enabled, layoutId, queryClient]);
};
