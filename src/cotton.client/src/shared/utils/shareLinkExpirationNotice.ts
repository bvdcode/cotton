import type { TFunction } from "i18next";
import {
  USER_PREFERENCE_KEYS,
  useUserPreferencesStore,
} from "../store/userPreferencesStore";
import { toast } from "../ui/notifications";
import { formatShareLinkExpiration } from "./shareLinkExpiration";

export const showShareLinkExpirationNotice = (
  expireAfterMinutes: number | null,
  t: TFunction,
): void => {
  const state = useUserPreferencesStore.getState();
  if (
    state.preferences[USER_PREFERENCE_KEYS.shareLinkExpirationNoticeShown] ===
    "true"
  ) {
    return;
  }

  toast.info(
    t("files:share.expirationNotice", {
      duration: formatShareLinkExpiration(expireAfterMinutes, t),
    }),
    { toastId: "share-link-expiration", autoClose: 12000 },
  );
  void state.updatePreferences({
    [USER_PREFERENCE_KEYS.shareLinkExpirationNoticeShown]: "true",
  });
};
