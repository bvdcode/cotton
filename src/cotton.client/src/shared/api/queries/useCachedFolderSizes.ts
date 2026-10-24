import { useMemo, useSyncExternalStore } from "react";
import type { FolderStatsDto } from "../nodesApi";
import { useAuthStore } from "../../store/authStore";
import { queryClient } from "./queryClient";
import { queryKeys } from "./queryKeys";

function createSizeStore(ids: readonly string[], userId: string) {
  const included = new Set(ids);
  let snapshot = new Map<string, number>();
  const synchronize = (id: string) => {
    const size = queryClient.getQueryData<FolderStatsDto>(
      queryKeys.nodeChildren.folderInfo(id, userId),
    )?.sizeBytes;
    if (snapshot.get(id) === size) {
      return false;
    }
    snapshot = new Map(snapshot);
    if (size === undefined) {
      snapshot.delete(id);
    } else {
      snapshot.set(id, size);
    }
    return true;
  };
  for (const id of included) {
    const size = queryClient.getQueryData<FolderStatsDto>(
      queryKeys.nodeChildren.folderInfo(id, userId),
    )?.sizeBytes;
    if (size !== undefined) {
      snapshot.set(id, size);
    }
  }
  return {
    getSnapshot: () => snapshot,
    subscribe: (notify: () => void) => {
      const unsubscribe = queryClient.getQueryCache().subscribe(({ query }) => {
        const key = query.queryKey;
        const id = key[1];
        if (
          key[0] === "nodeChildren" &&
          key[2] === userId &&
          key[3] === "folderInfo" &&
          typeof id === "string" &&
          included.has(id) &&
          synchronize(id)
        ) {
          notify();
        }
      });
      for (const id of included) {
        synchronize(id);
      }
      return unsubscribe;
    },
  };
}

export function useCachedFolderSizes(
  ids: readonly string[],
): ReadonlyMap<string, number> {
  const userId = useAuthStore((state) => state.user?.id ?? "");
  const store = useMemo(() => createSizeStore(ids, userId), [ids, userId]);
  return useSyncExternalStore(store.subscribe, store.getSnapshot);
}
