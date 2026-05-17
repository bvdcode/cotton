import { useCallback } from "react";
import { useQueryClient } from "@tanstack/react-query";
import {
  fetchServerSettings,
  invalidateServerSettings,
  useServerSettingsQuery,
} from "../api/queries/serverSettings";

export function useServerSettings() {
  const queryClient = useQueryClient();
  const query = useServerSettingsQuery();

  const fetchSettings = useCallback(
    async (options?: { force?: boolean }): Promise<void> => {
      if (options?.force) {
        await invalidateServerSettings(queryClient);
      }

      await fetchServerSettings(queryClient);
    },
    [queryClient],
  );

  return {
    data: query.data ?? null,
    loading: query.isFetching,
    loaded: query.isSuccess,
    error: query.isError ? "Failed to load settings" : null,
    lastUpdated: query.dataUpdatedAt || null,
    fetchSettings,
  };
}
