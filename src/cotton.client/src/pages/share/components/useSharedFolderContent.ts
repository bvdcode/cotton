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

const POST_UPLOAD_REFRESH_DELAYS_MS = [300, 2_000, 8_000];

const createPendingContentState = (
  nodeId: Guid | null,
): SharedFolderContentState => ({
  nodeId,
  content: null,
  loading: nodeId !== null,
  loadError: null,
});

export const useSharedFolderContent = (token: string, nodeId: Guid | null) => {
  const { t } = useTranslation(["share", "common"]);
  const [revision, setRevision] = React.useState(0);
  const refreshTimers = React.useRef<ReturnType<typeof setTimeout>[]>([]);
  const activeFolder = React.useRef<{
    token: string;
    nodeId: Guid | null;
  } | null>(null);
  const reload = React.useCallback(() => {
    if (
      activeFolder.current?.token !== token ||
      activeFolder.current.nodeId !== nodeId
    ) {
      return;
    }
    refreshTimers.current.forEach(clearTimeout);
    refreshTimers.current = POST_UPLOAD_REFRESH_DELAYS_MS.map((delay) =>
      setTimeout(() => setRevision((value) => value + 1), delay),
    );
  }, [nodeId, token]);
  React.useEffect(() => {
    activeFolder.current = { token, nodeId };
    return () => {
      activeFolder.current = null;
      refreshTimers.current.forEach(clearTimeout);
      refreshTimers.current = [];
    };
  }, [nodeId, token]);
  const [contentState, setContentState] =
    React.useState<SharedFolderContentState>(() =>
      createPendingContentState(nodeId),
    );

  React.useEffect(() => {
    if (!nodeId) {
      return;
    }

    let cancelled = false;

    void (async () => {
      try {
        const response = await sharedFoldersApi.getChildren(token, {
          nodeId,
          page: 1,
          pageSize: 1000,
        });

        if (cancelled) {
          return;
        }
        setContentState({
          nodeId,
          content: response.content,
          loading: false,
          loadError: null,
        });
      } catch {
        if (cancelled) {
          return;
        }
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
  }, [nodeId, t, token, revision]);

  return {
    ...(contentState.nodeId === nodeId
      ? contentState
      : createPendingContentState(nodeId)),
    reload,
  };
};
