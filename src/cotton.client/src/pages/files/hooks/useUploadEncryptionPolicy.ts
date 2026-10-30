import { useMemo } from "react";
import type { NodeDto } from "../../../shared/api/layoutsApi";
import type { NodeContentDto } from "../../../shared/api/nodesApi";
import {
  getFolderEncryptionPolicyStateFromParentResolver,
  useVault,
} from "../../../shared/crypto";
import { useNodesStore } from "../../../shared/store/nodesStore";

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
      (targetNodeId: string): { encrypt: boolean; vaultLocked: boolean } => {
        if (!isPolicyEnabledForNode(targetNodeId)) {
          return { encrypt: false, vaultLocked: false };
        }

        const vaultUnlocked = useVault.getState().isUnlocked;
        return { encrypt: vaultUnlocked, vaultLocked: !vaultUnlocked };
      },
    [isPolicyEnabledForNode],
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
