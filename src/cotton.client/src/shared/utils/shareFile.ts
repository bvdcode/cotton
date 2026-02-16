import type { TFunction } from "i18next";
import { filesApi } from "../api/filesApi";
import {
  selectShareLinkExpireAfterMinutes,
  useUserPreferencesStore,
} from "../store/userPreferencesStore";
import { shareLinks } from "./shareLinks";
import { shareLinkAction } from "./shareLinkAction";

interface ShareToast {
  open: boolean;
  message: string;
}

type SetShareToast = (toast: ShareToast) => void;

/**
 * Builds a formatted share text for a file.
 * Uses a localized invitation message followed by the URL.
 */
const buildShareMessage = (
  fileName: string,
  t: TFunction,
): string => t("share.message", { ns: "files", name: fileName });

/**
 * Shared logic for sharing a file link.
 * Extracts a share token from the download URL,
 * builds a share page URL, and tries Web Share API
 * with a formatted message. Falls back to clipboard.
 *
 * Deduplicated from FilesPage, TrashPage, SearchPage.
 */
export const shareFile = async (
  nodeFileId: string,
  fileName: string,
  t: TFunction,
  setShareToast: SetShareToast,
): Promise<void> => {
  try {
    const expireAfterMinutes =
      selectShareLinkExpireAfterMinutes(useUserPreferencesStore.getState());

    const downloadLink = await filesApi.getDownloadLink(
      nodeFileId,
      expireAfterMinutes,
    );

    const token = shareLinks.tryExtractTokenFromDownloadUrl(downloadLink);
    if (!token) {
      setShareToast({
        open: true,
        message: t("share.errors.token", { ns: "files" }),
      });
      return;
    }

    const url = shareLinks.buildShareUrl(token);
    const message = buildShareMessage(fileName, t);

    const outcome = await shareLinkAction({
      title: fileName,
      text: message,
      url,
    });

    switch (outcome.kind) {
      case "shared":
        setShareToast({
          open: true,
          message: t("share.shared", { ns: "files", name: fileName }),
        });
        return;
      case "copied":
        setShareToast({
          open: true,
          message: t("share.copied", { ns: "files", name: fileName }),
        });
        return;
      case "aborted":
        return;
      case "error":
      default:
        setShareToast({
          open: true,
          message: t("share.errors.copy", { ns: "files" }),
        });
    }
  } catch {
    setShareToast({
      open: true,
      message: t("share.errors.link", { ns: "files" }),
    });
  }
};
