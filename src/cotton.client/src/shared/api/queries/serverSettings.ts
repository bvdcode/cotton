import { useQuery, type QueryClient } from "@tanstack/react-query";
import { settingsApi, type ServerSettings } from "../settingsApi";
import { queryClient as defaultQueryClient } from "./queryClient";
import { queryKeys } from "./queryKeys";

const SERVER_SETTINGS_STALE_TIME_MS = 5 * 60_000;

const serverSettingsQueryOptions = () => ({
  queryKey: queryKeys.serverSettings.all(),
  queryFn: () => settingsApi.get(),
  staleTime: SERVER_SETTINGS_STALE_TIME_MS,
});

export const useServerSettingsQuery = (
  options: { enabled?: boolean } = {},
) =>
  useQuery<ServerSettings>({
    ...serverSettingsQueryOptions(),
    enabled: options.enabled ?? true,
  });

export const fetchServerSettings = (
  queryClient: QueryClient,
): Promise<ServerSettings> => queryClient.fetchQuery(serverSettingsQueryOptions());

export const invalidateServerSettings = async (
  queryClient: QueryClient,
): Promise<void> => {
  await queryClient.invalidateQueries({
    queryKey: queryKeys.serverSettings.all(),
  });
};

export const getCachedServerSettings = (
  queryClient: QueryClient = defaultQueryClient,
): ServerSettings | undefined =>
  queryClient.getQueryData<ServerSettings>(queryKeys.serverSettings.all());
