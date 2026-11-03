import { useMemo } from "react";
import type { NodeDto } from "../../../shared/api/layoutsApi";
import type { NodeContentDto } from "../../../shared/api/nodesApi";
import {
  getFolderEncryptionPolicyStateFromParentResolver,
  useVault,
} from "../../../shared/crypto";
import { useNodesStore } from "../../../shared/store/nodesStore";
import { useAuthStore } from "@shared/store/authStore";
import { queryClient } from "@shared/api/queries/queryClient";
import { ensureFolderOverview } from "@shared/api/queries/folderOverview";

export const useUploadEncryptionPolicy = () => {
  const isPolicyEnabledForNode = useMemo(
    () =>
      (targetNodeId: string): boolean => {
        const state = useNodesStore.getState();
        const target = findNodeById(state, targetNodeId);
        if (!target) return false;

        return getFolderEncryptionPolicyStateFromParentResolver(target, (id) =>
          findNodeById(state, id),
        ).effectiveEnabled;
      },
    [],
  );

  const decideEncrypt = useMemo(
    () =>
      async (
        targetNodeId: string,
      ): Promise<{ encrypt: boolean; vaultLocked: boolean }> => {
        const userId = useAuthStore.getState().user?.id;
        if (!userId || useNodesStore.getState().cacheOwnerUserId !== userId) {
          throw new Error("Folder metadata requires the current account");
        }
        const { content } = await ensureFolderOverview(
          queryClient,
          targetNodeId,
          userId,
        );
        const state = useNodesStore.getState();
        if (
          useAuthStore.getState().user?.id !== userId ||
          state.cacheOwnerUserId !== userId
        ) {
          throw new Error("The account changed while loading folder metadata");
        }
        const cached = findNodeById(state, targetNodeId);
        const target =
          cached &&
          Date.parse(cached.updatedAt) > Date.parse(content.node.updatedAt)
            ? cached
            : content.node;
        state.updateNode(target);
        const policy = getFolderEncryptionPolicyStateFromParentResolver(
          target,
          (id) => findNodeById(state, id),
        );
        if (!policy.effectiveEnabled) {
          return { encrypt: false, vaultLocked: false };
        }

        const vaultUnlocked = useVault.getState().isUnlocked;
        return { encrypt: vaultUnlocked, vaultLocked: !vaultUnlocked };
      },
    [],
  );

  return { isPolicyEnabledForNode, decideEncrypt };
};

type NodesStateView = {
  currentNode: NodeDto | null;
  ancestors: NodeDto[];
  contentByNodeId: Record<string, NodeContentDto | undefined>;
};

function findNodeById(state: NodesStateView, id: string): NodeDto | undefined {
  if (state.currentNode?.id === id) {
    return state.currentNode;
  }

  const ancestor = state.ancestors.find((node) => node.id === id);
  if (ancestor) {
    return ancestor;
  }

  for (const content of Object.values(state.contentByNodeId)) {
    const found = content?.nodes.find((node) => node.id === id);
    if (found) {
      return found;
    }
  }

  return undefined;
}
