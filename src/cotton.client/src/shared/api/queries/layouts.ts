import { useQuery, type QueryClient } from "@tanstack/react-query";
import { layoutsApi, type LayoutStatsDto, type NodeDto } from "../layoutsApi";
import type { NodeFileManifestDto } from "../nodesApi";
import { queryKeys } from "./queryKeys";

const DEFAULT_RECENT_COUNT = 15;

const requireLayoutId = (
  layoutId: string | null | undefined,
  queryName: string,
): string => {
  if (!layoutId) {
    throw new Error(`${queryName} requires a layoutId`);
  }

  return layoutId;
};

export const useRootNodeQuery = () =>
  useQuery<NodeDto>({
    queryKey: queryKeys.layouts.root(),
    queryFn: () => layoutsApi.resolve(),
  });

export const useLayoutStatsQuery = (layoutId: string | null | undefined) =>
  useQuery<LayoutStatsDto>({
    queryKey: queryKeys.layouts.stats(layoutId ?? ""),
    queryFn: () =>
      layoutsApi.getStats(requireLayoutId(layoutId, "useLayoutStatsQuery")),
    enabled: !!layoutId,
  });

export const useRecentFilesQuery = (
  layoutId: string | null | undefined,
  count = DEFAULT_RECENT_COUNT,
  filters?: {
    contentTypes?: readonly string[];
    excludedContentTypes?: readonly string[];
    enabled?: boolean;
  },
) =>
  useQuery<NodeFileManifestDto[]>({
    queryKey: queryKeys.layouts.recentFiltered(
      layoutId ?? "",
      count,
      filters?.contentTypes ?? [],
      filters?.excludedContentTypes ?? [],
    ),
    queryFn: () =>
      layoutsApi.getRecentFiles(
        requireLayoutId(layoutId, "useRecentFilesQuery"),
        count,
        {
          contentTypes: filters?.contentTypes,
          excludedContentTypes: filters?.excludedContentTypes,
        },
      ),
    enabled: Boolean(layoutId) && filters?.enabled !== false,
  });

export const usePinnedFoldersQuery = (
  nodeIds: readonly string[],
  enabled = true,
) =>
  useQuery<NodeDto[]>({
    queryKey: queryKeys.layouts.pinnedFolders(nodeIds),
    queryFn: () => layoutsApi.resolveOwnedNodes(nodeIds),
    enabled: enabled && nodeIds.length > 0,
  });

export const clearLayoutsCaches = (queryClient: QueryClient): void => {
  queryClient.removeQueries({ queryKey: queryKeys.layouts.all() });
};

export const invalidateLayoutOverview = async (
  queryClient: QueryClient,
  layoutId: string,
): Promise<void> => {
  await Promise.all([
    queryClient.invalidateQueries({
      queryKey: queryKeys.layouts.stats(layoutId),
    }),
    queryClient.invalidateQueries({
      queryKey: queryKeys.layouts.recentAll(layoutId),
    }),
  ]);
};
