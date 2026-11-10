import type { InfiniteData, QueryClient } from "@tanstack/react-query";
import type { NodeDto } from "../layoutsApi";
import type { NodeFileManifestDto, NodeResponse } from "../nodesApi";
import { applyDisplayMetaToFiles } from "../../crypto/displayMeta";
import { queryKeys } from "./queryKeys";
import { cachedFolderPages, type NodeNavigation } from "./nodeNavigation";

type ListingData = NodeResponse | InfiniteData<NodeResponse, number>;

export function updateCachedListings(
  client: QueryClient,
  userId: string,
  update: (page: NodeResponse, complete: boolean) => NodeResponse,
): void {
  const entries = client.getQueriesData<ListingData>({
    queryKey: ["nodeChildren"],
    predicate: ({ queryKey }) =>
      queryKey[2] === userId &&
      (queryKey[3] === "overview" ||
        queryKey[3] === "page" ||
        queryKey[3] === "complete" ||
        queryKey[3] === "tiles"),
  });
  for (const [key] of entries) {
    client.setQueryData<ListingData>(key, (data) => {
      if (!data) {
        return data;
      }
      if ("pages" in data) {
        const pages = data.pages.map((page) => update(page, false));
        return pages.every((page, index) => page === data.pages[index])
          ? data
          : { ...data, pages };
      }
      return update(data, key[3] === "complete");
    });
  }
}

export function addCachedFolder(
  client: QueryClient,
  userId: string,
  parentId: string,
  folder: NodeDto,
): void {
  updateCachedListings(client, userId, (page, complete) => {
    if (
      !complete ||
      page.content.node.id !== parentId ||
      page.content.nodes.some((node) => node.id === folder.id)
    ) {
      return page;
    }
    return {
      ...page,
      totalCount: page.totalCount + 1,
      content: { ...page.content, nodes: [...page.content.nodes, folder] },
    };
  });
}

export function upsertCachedFile(
  client: QueryClient,
  userId: string,
  parentId: string,
  file: NodeFileManifestDto,
): boolean {
  let updated = updateCachedFile(client, userId, parentId, file.id, () => file);
  updateCachedListings(client, userId, (page, complete) => {
    if (
      !complete ||
      page.content.node.id !== parentId ||
      page.content.files.some((item) => item.id === file.id)
    ) {
      return page;
    }
    updated = true;
    return {
      ...page,
      totalCount: page.totalCount + 1,
      content: { ...page.content, files: [...page.content.files, file] },
    };
  });
  return updated;
}

export function updateCachedNode(
  client: QueryClient,
  userId: string,
  updated: NodeDto,
): void {
  updateCachedListings(client, userId, (page) => {
    if (
      page.content.node.id !== updated.id &&
      !page.content.nodes.some((node) => node.id === updated.id)
    ) {
      return page;
    }
    return {
      ...page,
      content: {
        ...page.content,
        node: page.content.node.id === updated.id ? updated : page.content.node,
        nodes: page.content.nodes.map((node) =>
          node.id === updated.id ? updated : node,
        ),
      },
    };
  });
  client.setQueriesData<NodeNavigation>(
    {
      queryKey: queryKeys.nodeNavigation.all(),
      predicate: ({ queryKey }) => queryKey[2] === userId,
    },
    (view) =>
      view
        ? {
            node: view.node.id === updated.id ? updated : view.node,
            ancestors: view.ancestors.map((node) =>
              node.id === updated.id ? updated : node,
            ),
          }
        : view,
  );
}

export function invalidateCachedFolder(
  client: QueryClient,
  nodeId: string,
  userId: string,
): void {
  void client.invalidateQueries({
    queryKey: [...queryKeys.nodeChildren.all(nodeId), userId],
  });
}

export function removeCachedFolder(
  client: QueryClient,
  userId: string,
  nodeId: string,
): void {
  updateCachedListings(client, userId, (page) => {
    const nodes = page.content.nodes.filter((node) => node.id !== nodeId);
    return nodes.length === page.content.nodes.length
      ? page
      : {
          ...page,
          totalCount: Math.max(0, page.totalCount - 1),
          content: { ...page.content, nodes },
        };
  });
  client.removeQueries({
    queryKey: [...queryKeys.nodeChildren.all(nodeId), userId],
  });
  void client.invalidateQueries({
    queryKey: queryKeys.nodeNavigation.all(),
    predicate: ({ queryKey }) => queryKey[2] === userId,
  });
}

export function updateCachedFile(
  client: QueryClient,
  userId: string,
  parentId: string,
  fileId: string,
  update: (file: NodeFileManifestDto) => NodeFileManifestDto | null,
): boolean {
  let changed = false;
  updateCachedListings(client, userId, (page) => {
    if (page.content.node.id !== parentId) {
      return page;
    }
    let pageChanged = false;
    const files = page.content.files.flatMap((file) => {
      if (file.id !== fileId) {
        return [file];
      }
      const updated = update(file);
      pageChanged ||= updated !== file;
      changed ||= pageChanged;
      return updated ? [updated] : [];
    });
    if (!pageChanged) {
      return page;
    }
    return {
      ...page,
      totalCount: page.totalCount + files.length - page.content.files.length,
      content: { ...page.content, files },
    };
  });
  return changed;
}

export function findCachedFile(
  client: QueryClient,
  userId: string,
  parentId: string,
  fileId: string,
) {
  return cachedFolderPages(client, userId)
    .find(
      (page) =>
        page.content.node.id === parentId &&
        page.content.files.some((file) => file.id === fileId),
    )
    ?.content.files.find((file) => file.id === fileId);
}

export async function refreshCachedDisplayMetadata(
  client: QueryClient,
  userId: string,
): Promise<void> {
  const snapshots = client.getQueriesData<ListingData>({
    queryKey: ["nodeChildren"],
    predicate: ({ queryKey }) =>
      queryKey[2] === userId &&
      (queryKey[3] === "overview" ||
        queryKey[3] === "page" ||
        queryKey[3] === "complete" ||
        queryKey[3] === "tiles"),
  });
  await Promise.all(
    snapshots.map(async ([key, snapshot]) => {
      if (!snapshot) {
        return;
      }
      const decorate = async (page: NodeResponse) => ({
        ...page,
        content: {
          ...page.content,
          files: await applyDisplayMetaToFiles(page.content.files),
        },
      });
      const decorated =
        "pages" in snapshot
          ? {
              ...snapshot,
              pages: await Promise.all(snapshot.pages.map(decorate)),
            }
          : await decorate(snapshot);
      client.setQueryData<ListingData>(key, (current) =>
        current === snapshot ? decorated : current,
      );
    }),
  );
}
