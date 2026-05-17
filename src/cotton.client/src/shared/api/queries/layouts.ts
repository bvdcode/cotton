import { useQuery, type QueryClient } from "@tanstack/react-query";
import {
  layoutsApi,
  type LayoutStatsDto,
  type NodeDto,
} from "../layoutsApi";
import type { NodeFileManifestDto } from "../nodesApi";
import { queryKeys } from "./queryKeys";

const DEFAULT_RECENT_COUNT = 15;

export const useRootNodeQuery = () =>
  useQuery<NodeDto>({
    queryKey: queryKeys.layouts.root(),
    queryFn: () => layoutsApi.resolve(),
  });

export const useLayoutStatsQuery = (layoutId: string | null | undefined) =>
  useQuery<LayoutStatsDto>({
    queryKey: queryKeys.layouts.stats(layoutId ?? ""),
    queryFn: () => layoutsApi.getStats(layoutId as string),
    enabled: !!layoutId,
  });

export const useRecentFilesQuery = (
  layoutId: string | null | undefined,
  count = DEFAULT_RECENT_COUNT,
) =>
  useQuery<NodeFileManifestDto[]>({
    queryKey: queryKeys.layouts.recent(layoutId ?? "", count),
    queryFn: () => layoutsApi.getRecentFiles(layoutId as string, count),
    enabled: !!layoutId,
  });

export const clearLayoutsCaches = (queryClient: QueryClient): void => {
  queryClient.removeQueries({ queryKey: queryKeys.layouts.all() });
};
