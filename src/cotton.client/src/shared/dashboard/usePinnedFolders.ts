import { useCallback, useMemo } from "react";
import { useTranslation } from "react-i18next";
import { toast } from "../ui/notifications";
import {
  USER_PREFERENCE_KEYS,
  useUserPreferencesStore,
} from "../store/userPreferencesStore";
import {
  MAX_PINNED_FOLDERS,
  addPinnedFolder,
  parsePinnedFolderIds,
  removePinnedFolder,
  serializePinnedFolderIds,
} from "./pinnedFolders";

export const usePinnedFolders = () => {
  const { t } = useTranslation("home");
  const rawFolderIds = useUserPreferencesStore(
    (state) =>
      state.preferences[USER_PREFERENCE_KEYS.dashboardPinnedFolderIds],
  );
  const updatePreferences = useUserPreferencesStore(
    (state) => state.updatePreferences,
  );
  const folderIds = useMemo(
    () => parsePinnedFolderIds(rawFolderIds),
    [rawFolderIds],
  );
  const folderIdSet = useMemo(() => new Set(folderIds), [folderIds]);

  const isPinned = useCallback(
    (folderId: string): boolean => folderIdSet.has(folderId),
    [folderIdSet],
  );

  const setPinned = useCallback(
    (folderId: string, pinned: boolean): void => {
      if (pinned && !folderIdSet.has(folderId) && folderIds.length >= MAX_PINNED_FOLDERS) {
        toast.error(t("dashboard.pinnedFolders.limitReached", {
          count: MAX_PINNED_FOLDERS,
        }));
        return;
      }

      const nextFolderIds = pinned
        ? addPinnedFolder(folderIds, folderId)
        : removePinnedFolder(folderIds, folderId);
      if (nextFolderIds.length === folderIds.length
          && nextFolderIds.every((candidate, index) => candidate === folderIds[index])) {
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
    isPinned,
    setPinned,
    togglePinned,
  };
};
