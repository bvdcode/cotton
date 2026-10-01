import * as React from "react";
import { useTranslation } from "react-i18next";
import type { Guid } from "../../../shared/api/layoutsApi";
import {
  sharedFoldersApi,
  type SharedNodeContentDto,
} from "../../../shared/api/sharedFoldersApi";

interface SharedFolderContentState {
  nodeId: Guid | null;
  content: SharedNodeContentDto | null;
  loading: boolean;
  loadError: string | null;
}

const createPendingContentState = (
  nodeId: Guid | null,
): SharedFolderContentState => ({
  nodeId,
  content: null,
  loading: nodeId !== null,
  loadError: null,
});

export const useSharedFolderContent = (
  token: string,
  nodeId: Guid | null,
): SharedFolderContentState => {
  const { t } = useTranslation(["share", "common"]);
  const [contentState, setContentState] =
    React.useState<SharedFolderContentState>(() =>
      createPendingContentState(nodeId),
    );

  React.useEffect(() => {
    if (!nodeId) return;

    let cancelled = false;

    void (async () => {
      try {
        const response = await sharedFoldersApi.getChildren(token, {
          nodeId,
          page: 1,
          pageSize: 1000,
        });

        if (cancelled) return;
        setContentState({
          nodeId,
          content: response.content,
          loading: false,
          loadError: null,
        });
      } catch {
        if (cancelled) return;
        setContentState({
          nodeId,
          content: null,
          loading: false,
          loadError: t("errors.loadFailed", { ns: "share" }),
        });
      }
    })();

    return () => {
      cancelled = true;
    };
  }, [nodeId, t, token]);

  return contentState.nodeId === nodeId
    ? contentState
    : createPendingContentState(nodeId);
};
