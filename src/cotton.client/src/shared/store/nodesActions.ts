import { reportClientError } from "@shared/utils/clientDiagnostics";
import { queryClient } from "../api/queries/queryClient";
import { queryKeys } from "../api/queries/queryKeys";
import {
  NODE_NAVIGATION_STALE_TIME,
  nodeNavigationOptions,
  rootNodeOptions,
} from "../api/queries/nodeNavigation";
import { fetchAllNodeChildren } from "../api/nodeChildren";
import type { NodeDto } from "../api/layoutsApi";
import { isAxiosError } from "../api/httpClient";
import { translateError } from "../i18n/translateError";
import { useNodesStore } from "./nodesStore";
export { refreshNodeContent } from "./nodesContent";
export { createFolder, deleteFolder, renameFolder } from "./nodesFolderActions";

interface LoadNodeOptions {
  loadChildren?: boolean;
  allowRootRecovery?: boolean;
  force?: boolean;
}

function beginNavigation() {
  const state = useNodesStore.getState();
  const owner = state.cacheOwnerUserId;
  const version = state.navigationVersion + 1;
  useNodesStore.setState({
    navigationVersion: version,
    loading: true,
    error: null,
  });
  return {
    owner,
    isCurrent: () => {
      const current = useNodesStore.getState();
      return (
        current.cacheOwnerUserId === owner &&
        current.navigationVersion === version
      );
    },
  };
}

async function resolveRoot(userId: string, force = false): Promise<NodeDto> {
  const options = rootNodeOptions(userId);
  const root = await queryClient.fetchQuery({
    ...options,
    staleTime: force ? 0 : options.staleTime,
  });
  return root;
}

export async function loadRoot(
  options?: LoadNodeOptions,
): Promise<NodeDto | null> {
  const navigation = beginNavigation();
  if (!navigation.owner) {
    useNodesStore.setState({ loading: false });
    return null;
  }
  try {
    const root = await resolveRoot(navigation.owner, options?.force);
    if (!navigation.isCurrent()) {
      return null;
    }
    useNodesStore.setState({ rootNodeId: root.id });
    queryClient.setQueryData(
      queryKeys.nodeNavigation.view(root.id, navigation.owner),
      { node: root, ancestors: [] },
    );
    await loadNode(root.id, options);
    return root;
  } catch (error) {
    if (navigation.isCurrent()) {
      reportClientError("Failed to resolve root node", error);
      useNodesStore.setState({
        loading: false,
        error: translateError("files", "errors.resolveRootFailed"),
      });
    }
    return null;
  }
}

export async function loadNode(
  nodeId: string,
  options?: LoadNodeOptions,
): Promise<void> {
  const navigation = beginNavigation();
  if (!navigation.owner) {
    useNodesStore.setState({ loading: false });
    return;
  }
  const owner = navigation.owner;
  useNodesStore.setState({ currentNodeId: nodeId });
  try {
    if (options?.force) {
      await Promise.all([
        queryClient.cancelQueries({
          queryKey: queryKeys.nodeNavigation.view(nodeId, owner),
          exact: true,
        }),
        queryClient.cancelQueries({
          queryKey: queryKeys.nodeChildren.complete(nodeId, owner),
          exact: true,
        }),
      ]);
      if (!navigation.isCurrent()) {
        return;
      }
    }
    await queryClient.fetchQuery({
      ...nodeNavigationOptions(nodeId, owner, options?.force),
      staleTime: options?.force ? 0 : NODE_NAVIGATION_STALE_TIME,
    });
    if (options?.loadChildren !== false && navigation.isCurrent()) {
      await queryClient.fetchQuery({
        queryKey: queryKeys.nodeChildren.complete(nodeId, owner),
        queryFn: ({ signal }) => fetchAllNodeChildren(nodeId, { signal }),
        staleTime: options?.force ? 0 : NODE_NAVIGATION_STALE_TIME,
      });
    }
    if (navigation.isCurrent()) {
      useNodesStore.setState({ loading: false, error: null });
    }
  } catch (error) {
    if (!navigation.isCurrent()) {
      return;
    }
    const status = isAxiosError(error) ? error.response?.status : undefined;
    if (
      status === 404 &&
      options?.allowRootRecovery !== false &&
      useNodesStore.getState().rootNodeId === nodeId
    ) {
      useNodesStore.setState({ rootNodeId: null, currentNodeId: null });
      await loadRoot({ ...options, force: true, allowRootRecovery: false });
      return;
    }
    reportClientError("Failed to load node view", error);
    useNodesStore.setState({
      loading: false,
      error: translateError(
        "files",
        status === 404 ? "errors.folderNotFound" : "errors.loadContentsFailed",
      ),
    });
  }
}

export function resolveRootInBackground(options?: LoadNodeOptions): void {
  const snapshot = useNodesStore.getState();
  const owner = snapshot.cacheOwnerUserId;
  if (!owner || !snapshot.rootNodeId) {
    return;
  }
  void resolveRoot(owner, options?.force)
    .then(async (root) => {
      const state = useNodesStore.getState();
      if (
        state.cacheOwnerUserId !== owner ||
        state.navigationVersion !== snapshot.navigationVersion
      ) {
        return;
      }
      useNodesStore.setState({ rootNodeId: root.id });
      if (
        root.id !== snapshot.rootNodeId &&
        state.currentNodeId === snapshot.rootNodeId
      ) {
        queryClient.setQueryData(
          queryKeys.nodeNavigation.view(root.id, owner),
          { node: root, ancestors: [] },
        );
        await loadNode(root.id, options);
      }
    })
    .catch((error: Error) =>
      reportClientError("Failed to resolve root node in background", error),
    );
}
