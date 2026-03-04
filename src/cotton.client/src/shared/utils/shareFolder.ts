import type { TFunction } from "i18next";
import { nodesApi } from "../api/nodesApi";
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

const buildShareMessage = (
  folderName: string,
  t: TFunction,
): string => t("share.folderMessage", { ns: "files", name: folderName });

export const shareFolder = async (
  nodeId: string,
  folderName: string,
  t: TFunction,
  setShareToast: SetShareToast,
): Promise<void> => {
  try {
    const expireAfterMinutes =
      selectShareLinkExpireAfterMinutes(useUserPreferencesStore.getState());

    const token = await nodesApi.getShareLink(nodeId, expireAfterMinutes);
    if (!token) {
      setShareToast({
        open: true,
        message: t("share.errors.token", { ns: "files" }),
      });
      return;
    }

    const url = shareLinks.buildShareUrl(token);
    const message = buildShareMessage(folderName, t);

    const outcome = await shareLinkAction({
      title: folderName,
      text: message,
      url,
    });

    switch (outcome.kind) {
      case "shared":
        setShareToast({
          open: true,
          message: t("share.shared", { ns: "files", name: folderName }),
        });
        return;
      case "copied":
        setShareToast({
          open: true,
          message: t("share.copied", { ns: "files", name: folderName }),
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
