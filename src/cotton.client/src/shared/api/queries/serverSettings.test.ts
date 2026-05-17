import { QueryClient } from "@tanstack/react-query";
import { afterEach, describe, expect, it, vi } from "vitest";
import { settingsApi, type ServerSettings } from "../settingsApi";
import {
  fetchServerSettings,
  getCachedServerSettings,
  invalidateServerSettings,
} from "./serverSettings";
import { queryClient as defaultQueryClient } from "./queryClient";
import { queryKeys } from "./queryKeys";

const sampleSettings: ServerSettings = {
  version: "1.0.0",
  maxChunkSizeBytes: 1024,
  supportedHashAlgorithm: "SHA-256",
};

const createQueryClient = () =>
  new QueryClient({
    defaultOptions: {
      queries: { retry: false },
      mutations: { retry: false },
    },
  });

afterEach(() => {
  vi.restoreAllMocks();
  defaultQueryClient.removeQueries({ queryKey: queryKeys.serverSettings.all() });
});

describe("server settings query helpers", () => {
  it("fetches settings through the shared query options", async () => {
    const get = vi.spyOn(settingsApi, "get").mockResolvedValue(sampleSettings);
    const queryClient = createQueryClient();

    await expect(fetchServerSettings(queryClient)).resolves.toEqual(
      sampleSettings,
    );

    expect(get).toHaveBeenCalledTimes(1);
    expect(
      queryClient.getQueryData(queryKeys.serverSettings.all()),
    ).toEqual(sampleSettings);
  });

  it("returns cached settings from an explicit client", () => {
    const queryClient = createQueryClient();
    queryClient.setQueryData(queryKeys.serverSettings.all(), sampleSettings);

    expect(getCachedServerSettings(queryClient)).toEqual(sampleSettings);
  });

  it("returns cached settings from the default client", () => {
    defaultQueryClient.setQueryData(
      queryKeys.serverSettings.all(),
      sampleSettings,
    );

    expect(getCachedServerSettings()).toEqual(sampleSettings);
  });

  it("returns undefined when settings have not been cached", () => {
    expect(getCachedServerSettings(createQueryClient())).toBeUndefined();
  });

  it("invalidates the server settings cache", async () => {
    const queryClient = createQueryClient();
    queryClient.setQueryData(queryKeys.serverSettings.all(), sampleSettings);

    await invalidateServerSettings(queryClient);

    expect(
      queryClient
        .getQueryCache()
        .find({ queryKey: queryKeys.serverSettings.all() })?.state
        .isInvalidated,
    ).toBe(true);
  });
});
