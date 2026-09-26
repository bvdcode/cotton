import { nodesApi, type NodeContentDto } from "../api/nodesApi";
import { layoutsApi, type NodeDto } from "../api/layoutsApi";
import { isAxiosError } from "../api/httpClient";
import { translateError } from "../i18n/translateError";
import {
  ROOT_RESOLVE_MIN_INTERVAL_MS,
  rootResolveState,
} from "./nodesActionInternals";
import { useNodesStore } from "./nodesStore";
import { fetchAllNodeChildren } from "./nodesContent";
export { refreshNodeContent } from "./nodesContent";
export { createFolder, deleteFolder, renameFolder } from "./nodesFolderActions";

const tFileError = (key: string): string => translateError("files", key);

type ResolveSnapshot = {
  currentNode: NodeDto | null;
  ancestors: NodeDto[];
  contentByNodeId: Record<string, NodeContentDto | undefined>;
  ancestorsByNodeId: Record<string, NodeDto[] | undefined>;
};

async function resolveNodeAndAncestors(
  nodeId: string,
  state: ResolveSnapshot,
): Promise<{ node: NodeDto; ancestors: NodeDto[] }> {
  type LocalResolution = {
    node: NodeDto | null;
    ancestors: NodeDto[] | null;
  };

  const getCachedAncestors = (): NodeDto[] | null =>
    state.ancestorsByNodeId[nodeId] ?? null;

  const tryResolveFromCurrentNode = (): LocalResolution | null => {
    if (state.currentNode?.id !== nodeId) return null;
    return {
      node: state.currentNode,
      ancestors: getCachedAncestors() ?? state.ancestors,
    };
  };

  const tryResolveFromAncestors = (): LocalResolution | null => {
    const index = state.ancestors.findIndex((item) => item.id === nodeId);
    if (index < 0) return null;
    return {
      node: state.ancestors[index],
      ancestors: getCachedAncestors() ?? state.ancestors.slice(0, index),
    };
  };

  const tryResolveFromCurrentChildren = (): LocalResolution | null => {
    if (!state.currentNode) return null;
    const parentContent = state.contentByNodeId[state.currentNode.id];
    const found =
      parentContent?.nodes.find((item) => item.id === nodeId) ?? null;
    if (!found) return null;

    const ancestors = getCachedAncestors();
    if (ancestors) {
      return { node: found, ancestors };
    }

    if (found.parentId === state.currentNode.id) {
      return {
        node: found,
        ancestors: [...state.ancestors, state.currentNode],
      };
    }

    return { node: found, ancestors: null };
  };

  const tryResolveFromAnyCachedContent = (): LocalResolution | null => {
    for (const content of Object.values(state.contentByNodeId)) {
      if (!content) continue;
      const found = content.nodes.find((n) => n.id === nodeId);
      if (found) {
        return { node: found, ancestors: getCachedAncestors() };
      }
    }
    return null;
  };

  const local =
    tryResolveFromCurrentNode() ??
    tryResolveFromAncestors() ??
    tryResolveFromCurrentChildren() ??
    tryResolveFromAnyCachedContent();

  const node = local?.node ?? (await nodesApi.getNode(nodeId));
  const ancestors = local?.ancestors ?? (await nodesApi.getAncestors(nodeId));

  return { node, ancestors };
}

const scheduleRootResolve = (options?: {
  loadChildren?: boolean;
  force?: boolean;
}): void => {
  const existingRootId = useNodesStore.getState().rootNodeId;
  if (!existingRootId) return;

  if (rootResolveState.promise) return;

  const force = options?.force ?? false;
  if (!force) {
    const elapsedSinceLastResolve = Date.now() - rootResolveState.lastStartedAt;
    if (elapsedSinceLastResolve < ROOT_RESOLVE_MIN_INTERVAL_MS) {
      return;
    }
  }

  const loadChildren = options?.loadChildren ?? true;
  rootResolveState.lastStartedAt = Date.now();

  const ownerAtSchedule = useNodesStore.getState().cacheOwnerUserId;
  const isStillSameOwner = (): boolean =>
    useNodesStore.getState().cacheOwnerUserId === ownerAtSchedule;

  const currentPromise = (async () => {
    try {
      const root = await layoutsApi.resolve();
      if (!isStillSameOwner()) return;

      const state = useNodesStore.getState();
      if (state.rootNodeId === root.id) {
        return;
      }

      const previousRootId = state.rootNodeId;
      useNodesStore.setState({ rootNodeId: root.id });

      const isViewingRoot =
        state.currentNode == null || state.currentNode.id === previousRootId;
      if (isViewingRoot && isStillSameOwner()) {
        await loadNode(root.id, {
          loadChildren,
          allowRootRecovery: false,
        });
      }
    } catch (error) {
      console.error("Failed to resolve root node in background", error);
    }
  })();

  rootResolveState.promise = currentPromise;
  void currentPromise.finally(() => {
    if (rootResolveState.promise === currentPromise) {
      rootResolveState.promise = null;
    }
  });
};

export const loadRoot = async (options?: {
  force?: boolean;
  loadChildren?: boolean;
}): Promise<NodeDto | null> => {
  const force = options?.force ?? false;
  const loadChildren = options?.loadChildren ?? true;
  const state = useNodesStore.getState();

  if (!force && state.rootNodeId) {
    const hasCachedContent = state.contentByNodeId[state.rootNodeId];
    if (!loadChildren || hasCachedContent) {
      await loadNode(state.rootNodeId, { loadChildren });
      scheduleRootResolve({ loadChildren });
      return useNodesStore.getState().currentNode;
    }
  }

  try {
    const root = await layoutsApi.resolve();
    useNodesStore.setState({ rootNodeId: root.id });
    await loadNode(root.id, { loadChildren });
    return root;
  } catch (error) {
    console.error("Failed to resolve root node", error);
    useNodesStore.setState({
      loading: false,
      error: tFileError("errors.resolveRootFailed"),
    });
    return null;
  }
};

type LoadNodeOptions = {
  loadChildren: boolean;
  allowRootRecovery: boolean;
  force: boolean;
};

const resolveLoadNodeOptions = (options?: {
  loadChildren?: boolean;
  allowRootRecovery?: boolean;
  force?: boolean;
}): LoadNodeOptions => ({
  loadChildren: options?.loadChildren ?? true,
  allowRootRecovery: options?.allowRootRecovery ?? true,
  force: options?.force ?? false,
});

const updateLoadState = (
  nodeId: string,
  force: boolean,
): NodeContentDto | undefined => {
  const cachedContent = force
    ? undefined
    : useNodesStore.getState().contentByNodeId[nodeId];

  useNodesStore.setState(
    cachedContent ? { error: null } : { loading: true, error: null },
  );
  return cachedContent;
};

const refreshChildrenInBackground = (
  nodeId: string,
  cachedContent: NodeContentDto | undefined,
  loadChildren: boolean,
): void => {
  if (!loadChildren || !cachedContent) return;

  void (async () => {
    try {
      const fresh = await fetchAllNodeChildren(nodeId);
      useNodesStore.setState((prev) => ({
        contentByNodeId: {
          ...prev.contentByNodeId,
          [nodeId]: fresh,
        },
        lastUpdatedByNodeId: {
          ...prev.lastUpdatedByNodeId,
          [nodeId]: Date.now(),
        },
      }));
    } catch {
      // Cached content is still usable if the background refresh fails.
    }
  })();
};

const tryRecoverRootNodeAsync = async (
  nodeId: string,
  statusCode: number | undefined,
  options: LoadNodeOptions,
): Promise<boolean> => {
  if (
    !shouldAttemptRootRecovery(nodeId, statusCode, options.allowRootRecovery)
  ) {
    return false;
  }

  try {
    clearRootCache();
    const root = await layoutsApi.resolve();
    useNodesStore.setState({ rootNodeId: root.id });
    await loadNode(root.id, {
      loadChildren: options.loadChildren,
      allowRootRecovery: false,
    });
    return true;
  } catch (recoveryError) {
    console.error("Failed to recover root node", recoveryError);
    useNodesStore.setState({
      loading: false,
      error: tFileError("errors.resolveRootFailed"),
    });
    return true;
  }
};

const shouldAttemptRootRecovery = (
  nodeId: string,
  statusCode: number | undefined,
  allowRootRecovery: boolean,
): boolean =>
  allowRootRecovery &&
  statusCode === 404 &&
  useNodesStore.getState().rootNodeId === nodeId;

const clearRootCache = () => {
  useNodesStore.setState({
    rootNodeId: null,
    currentNode: null,
    ancestors: [],
    contentByNodeId: {},
    ancestorsByNodeId: {},
    lastUpdatedByNodeId: {},
  });
};

const resolveNodeViewAsync = async (
  nodeId: string,
  state: ResolveSnapshot,
  force: boolean,
): Promise<{ node: NodeDto; ancestors: NodeDto[] }> =>
  force
    ? {
        node: await nodesApi.getNode(nodeId),
        ancestors: await nodesApi.getAncestors(nodeId),
      }
    : resolveNodeAndAncestors(nodeId, state);

const resolveNodeContentAsync = async (
  nodeId: string,
  cachedContent: NodeContentDto | undefined,
  loadChildren: boolean,
): Promise<NodeContentDto | undefined> => {
  if (!loadChildren) {
    return undefined;
  }

  return cachedContent ?? fetchAllNodeChildren(nodeId);
};

const applyLoadedNodeState = (
  nodeId: string,
  resolved: { node: NodeDto; ancestors: NodeDto[] },
  content: NodeContentDto | undefined,
  loadChildren: boolean,
) => {
  useNodesStore.setState((prev) => ({
    currentNode: resolved.node,
    ancestors: resolved.ancestors,
    ancestorsByNodeId: {
      ...prev.ancestorsByNodeId,
      [nodeId]: resolved.ancestors,
    },
    contentByNodeId: loadChildren
      ? {
          ...prev.contentByNodeId,
          [nodeId]: content,
        }
      : prev.contentByNodeId,
    lastUpdatedByNodeId: {
      ...prev.lastUpdatedByNodeId,
      [nodeId]: Date.now(),
    },
    loading: false,
    error: null,
  }));
};

const scheduleRootResolveIfNeeded = (
  nodeId: string,
  options: LoadNodeOptions,
) => {
  if (
    options.allowRootRecovery &&
    useNodesStore.getState().rootNodeId === nodeId
  ) {
    scheduleRootResolve({ loadChildren: options.loadChildren });
  }
};

const getLoadNodeErrorMessage = (statusCode: number | undefined): string =>
  statusCode === 404
    ? tFileError("errors.folderNotFound")
    : tFileError("errors.loadContentsFailed");

const handleLoadNodeFailureAsync = async (
  nodeId: string,
  error: unknown,
  options: LoadNodeOptions,
): Promise<void> => {
  const statusCode = isAxiosError(error) ? error.response?.status : undefined;
  const recovered = await tryRecoverRootNodeAsync(nodeId, statusCode, options);
  if (recovered) return;

  console.error("Failed to load node view", error);
  useNodesStore.setState({
    loading: false,
    error: getLoadNodeErrorMessage(statusCode),
  });
};

export const loadNode = async (
  nodeId: string,
  options?: {
    loadChildren?: boolean;
    allowRootRecovery?: boolean;
    force?: boolean;
  },
): Promise<void> => {
  const state = useNodesStore.getState();
  const resolvedOptions = resolveLoadNodeOptions(options);
  if (state.loading && state.currentNode?.id === nodeId) return;

  const cachedContent = updateLoadState(nodeId, resolvedOptions.force);
  try {
    const resolved = await resolveNodeViewAsync(
      nodeId,
      state,
      resolvedOptions.force,
    );
    const content = await resolveNodeContentAsync(
      nodeId,
      cachedContent,
      resolvedOptions.loadChildren,
    );

    applyLoadedNodeState(
      nodeId,
      resolved,
      content,
      resolvedOptions.loadChildren,
    );
    refreshChildrenInBackground(
      nodeId,
      cachedContent,
      resolvedOptions.loadChildren,
    );
    scheduleRootResolveIfNeeded(nodeId, resolvedOptions);
  } catch (error) {
    await handleLoadNodeFailureAsync(nodeId, error, resolvedOptions);
  }
};

export const resolveRootInBackground = (options?: {
  loadChildren?: boolean;
  force?: boolean;
}): void => {
  scheduleRootResolve(options);
};
