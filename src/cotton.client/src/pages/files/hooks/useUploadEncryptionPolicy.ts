import { findCachedNode } from "../../../shared/api/queries/nodeNavigation";
import { useMemo } from "react";
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
        const owner = useNodesStore.getState().cacheOwnerUserId;
        const target = owner
          ? findCachedNode(queryClient, owner, targetNodeId)
          : undefined;
        if (!target) {
          return false;
        }

        return getFolderEncryptionPolicyStateFromParentResolver(target, (id) =>
          owner ? findCachedNode(queryClient, owner, id) : undefined,
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
        const cached = findCachedNode(queryClient, userId, targetNodeId);
        const target =
          cached &&
          Date.parse(cached.updatedAt) > Date.parse(content.node.updatedAt)
            ? cached
            : content.node;
        state.updateNode(target);
        const policy = getFolderEncryptionPolicyStateFromParentResolver(
          target,
          (id) => findCachedNode(queryClient, userId, id),
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
