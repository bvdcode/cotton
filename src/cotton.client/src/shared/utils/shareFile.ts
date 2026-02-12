import type { TFunction } from "i18next";
import { filesApi } from "../api/filesApi";
import { usePreferencesStore } from "../store/preferencesStore";
import { shareLinks } from "./shareLinks";

interface ShareToast {
  open: boolean;
  message: string;
}

type SetShareToast = (toast: ShareToast) => void;

/**
 * Builds a formatted share text for a file.
 * Uses a localized invitation message followed by the URL.
 */
const buildShareText = (fileName: string, url: string, t: TFunction): string => {
  const message = t("share.message", { ns: "files", name: fileName });
  return `${message}\n\n${url}`;
};

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
      usePreferencesStore.getState().shareLinkPreferences.expireAfterMinutes;

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
    const shareText = buildShareText(fileName, url, t);

    if (
      typeof navigator !== "undefined" &&
      typeof navigator.share === "function"
    ) {
      try {
        await navigator.share({
          title: fileName,
          text: shareText,
          url,
        });
        setShareToast({
          open: true,
          message: t("share.shared", { ns: "files", name: fileName }),
        });
        return;
      } catch (e) {
        if (e instanceof Error && e.name === "AbortError") {
          return;
        }
      }
    }

    try {
      await navigator.clipboard.writeText(shareText);
      setShareToast({
        open: true,
        message: t("share.copied", { ns: "files", name: fileName }),
      });
    } catch {
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
