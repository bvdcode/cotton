import { useQueryClient } from "@tanstack/react-query";
import { useFileTreeRealtimeInvalidation } from "../../signalr";
import { HUB_METHODS } from "../../signalr/hubMethods";
import { invalidateFolderInfoCache } from "./folderInfo";

export const useFolderSizeInvalidation = (enabled: boolean): void => {
  const client = useQueryClient();
  useFileTreeRealtimeInvalidation({
    enabled,
    onInvalidate: () => void invalidateFolderInfoCache(client),
    shouldInvalidate: (method) =>
      method !== HUB_METHODS.FileRenamed &&
      method !== HUB_METHODS.NodeRenamed &&
      method !== HUB_METHODS.NodeMetadataUpdated,
  });
};
