import { useCallback, useEffect, useMemo, useRef } from "react";
import { useTranslation } from "react-i18next";
import { usePinnedFoldersQuery } from "../api/queries/layouts";
import { toast } from "../ui/notifications";
import {
  USER_PREFERENCE_KEYS,
  useUserPreferencesStore,
} from "../store/userPreferencesStore";
import {
  MAX_PINNED_FOLDERS,
  addPinnedFolder,
  parsePinnedFolderIds,
  removeMissingPinnedFolders,
  removePinnedFolder,
  serializePinnedFolderIds,
} from "./pinnedFolders";

export const usePinnedFolders = () => {
  const { t } = useTranslation("home");
  const rawFolderIds = useUserPreferencesStore(
    (state) => state.preferences[USER_PREFERENCE_KEYS.dashboardPinnedFolderIds],
  );
  const updatePreferences = useUserPreferencesStore(
    (state) => state.updatePreferences,
  );
  const folderIds = useMemo(
    () => parsePinnedFolderIds(rawFolderIds),
    [rawFolderIds],
  );
  const folderIdSet = useMemo(() => new Set(folderIds), [folderIds]);
  const foldersQuery = usePinnedFoldersQuery(folderIds);
  const missingCleanupAttemptRef = useRef<{
    key: string;
    target: string;
  } | null>(null);

  useEffect(() => {
    if (!foldersQuery.isSuccess) {
      return;
    }

    const resolvedFolderIds = (foldersQuery.data ?? []).map(
      (folder) => folder.id,
    );
    const nextFolderIds = removeMissingPinnedFolders(
      folderIds,
      resolvedFolderIds,
    );
    if (nextFolderIds.length === folderIds.length) {
      const current = serializePinnedFolderIds(folderIds);
      if (missingCleanupAttemptRef.current?.target !== current) {
        missingCleanupAttemptRef.current = null;
      }
      return;
    }

    const attemptKey = JSON.stringify([
      folderIds,
      resolvedFolderIds,
      foldersQuery.dataUpdatedAt,
    ]);
    if (missingCleanupAttemptRef.current?.key === attemptKey) {
      return;
    }
    const serializedNextFolderIds = serializePinnedFolderIds(nextFolderIds);
    missingCleanupAttemptRef.current = {
      key: attemptKey,
      target: serializedNextFolderIds,
    };

    void updatePreferences({
      [USER_PREFERENCE_KEYS.dashboardPinnedFolderIds]: serializedNextFolderIds,
    });
  }, [
    folderIds,
    foldersQuery.data,
    foldersQuery.dataUpdatedAt,
    foldersQuery.isSuccess,
    updatePreferences,
  ]);

  const isPinned = useCallback(
    (folderId: string): boolean => folderIdSet.has(folderId),
    [folderIdSet],
  );

  const setPinned = useCallback(
    (folderId: string, pinned: boolean): void => {
      if (
        pinned &&
        !folderIdSet.has(folderId) &&
        folderIds.length >= MAX_PINNED_FOLDERS
      ) {
        toast.error(
          t("dashboard.pinnedFolders.limitReached", {
            count: MAX_PINNED_FOLDERS,
          }),
        );
        return;
      }

      const nextFolderIds = pinned
        ? addPinnedFolder(folderIds, folderId)
        : removePinnedFolder(folderIds, folderId);
      if (
        nextFolderIds.length === folderIds.length &&
        nextFolderIds.every(
          (candidate, index) => candidate === folderIds[index],
        )
      ) {
        return;
      }

      void updatePreferences({
        [USER_PREFERENCE_KEYS.dashboardPinnedFolderIds]:
          serializePinnedFolderIds(nextFolderIds),
      });
    },
    [folderIdSet, folderIds, t, updatePreferences],
  );

  const togglePinned = useCallback(
    (folderId: string): void => {
      setPinned(folderId, !folderIdSet.has(folderId));
    },
    [folderIdSet, setPinned],
  );

  return {
    folderIds,
    folders: foldersQuery.data ?? [],
    foldersError: foldersQuery.isError,
    foldersPending: foldersQuery.isPending,
    isPinned,
    refetchFolders: foldersQuery.refetch,
    setPinned,
    togglePinned,
  };
};
