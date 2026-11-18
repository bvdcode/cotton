import type { Query, QueryClient } from "@tanstack/react-query";
import { queryKeys } from "./queryKeys";

export const FOLDER_REFRESH_DELAY_MS = 1000;

interface RefreshBatch {
  queries: Set<Query>;
  waiters: (() => void)[];
  timer: ReturnType<typeof setTimeout> | null;
  running: boolean;
}

const pending = new WeakMap<QueryClient, RefreshBatch>();

export function refreshFolderListings(
  client: QueryClient,
  nodeId: string,
  userId?: string,
): Promise<void> {
  const queries = client.getQueryCache().findAll({
    queryKey: queryKeys.nodeChildren.all(nodeId),
    predicate: ({ queryKey }) =>
      (userId === undefined || queryKey[2] === userId) &&
      queryKey[3] !== "folderInfo" &&
      queryKey[3] !== "folderSizes",
  });
  if (queries.length === 0) {
    return Promise.resolve();
  }

  const selected = new Set(queries);
  void client.invalidateQueries({
    predicate: (query) => selected.has(query),
    refetchType: "none",
  });
  let batch = pending.get(client);
  if (!batch) {
    batch = { queries: new Set(), waiters: [], timer: null, running: false };
    pending.set(client, batch);
  }
  for (const query of queries) {
    batch.queries.add(query);
  }
  const completed = new Promise<void>((resolve) => batch.waiters.push(resolve));
  schedule(client, batch);
  return completed;
}

function schedule(client: QueryClient, batch: RefreshBatch): void {
  if (batch.timer !== null || batch.running) {
    return;
  }
  batch.timer = setTimeout(
    () => void flush(client, batch),
    FOLDER_REFRESH_DELAY_MS,
  );
}

async function flush(client: QueryClient, batch: RefreshBatch): Promise<void> {
  batch.timer = null;
  batch.running = true;
  const queries = new Set(batch.queries);
  const waiters = batch.waiters.splice(0);
  batch.queries.clear();
  try {
    await Promise.allSettled(
      [...queries].flatMap((query) => (query.promise ? [query.promise] : [])),
    );
    if (pending.get(client) === batch) {
      await client.invalidateQueries(
        { predicate: (query) => queries.has(query) },
        { cancelRefetch: false },
      );
    }
  } finally {
    for (const resolve of waiters) {
      resolve();
    }
    batch.running = false;
    if (pending.get(client) === batch) {
      if (batch.queries.size > 0) {
        schedule(client, batch);
      } else {
        pending.delete(client);
      }
    }
  }
}

export function clearFolderListingRefresh(client: QueryClient): void {
  const batch = pending.get(client);
  if (!batch) {
    return;
  }
  if (batch.timer !== null) {
    clearTimeout(batch.timer);
  }
  pending.delete(client);
  for (const resolve of batch.waiters) {
    resolve();
  }
}
