import { useMemo, useSyncExternalStore } from "react";
import type { FolderStatsDto } from "../nodesApi";
import { useAuthStore } from "../../store/authStore";
import { queryClient } from "./queryClient";
import { queryKeys } from "./queryKeys";

function createSizeStore(ids: readonly string[], userId: string) {
  const included = new Set(ids);
  let snapshot = new Map<string, number>();
  const synchronize = (changedIds: Iterable<string>) => {
    let next: Map<string, number> | undefined;
    for (const id of changedIds) {
      const size = queryClient.getQueryData<FolderStatsDto>(
        queryKeys.nodeChildren.folderInfo(id, userId),
      )?.sizeBytes;
      if (snapshot.get(id) === size) {
        continue;
      }
      next ??= new Map(snapshot);
      if (size === undefined) {
        next.delete(id);
      } else {
        next.set(id, size);
      }
    }
    if (!next) {
      return false;
    }
    snapshot = next;
    return true;
  };
  synchronize(included);
  return {
    getSnapshot: () => snapshot,
    subscribe: (notify: () => void) => {
      const pending = new Set<string>();
      let active = true;
      let scheduled = false;
      const flush = () => {
        scheduled = false;
        if (!active) {
          return;
        }
        const changed = synchronize(pending);
        pending.clear();
        if (changed) {
          notify();
        }
      };
      const unsubscribe = queryClient.getQueryCache().subscribe(({ query }) => {
        const key = query.queryKey;
        const id = key[1];
        if (
          key[0] === "nodeChildren" &&
          key[2] === userId &&
          key[3] === "folderInfo" &&
          typeof id === "string" &&
          included.has(id)
        ) {
          pending.add(id);
          if (!scheduled) {
            scheduled = true;
            queueMicrotask(flush);
          }
        }
      });
      synchronize(included);
      return () => {
        active = false;
        pending.clear();
        unsubscribe();
      };
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
