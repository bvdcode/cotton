import type { TFunction } from "i18next";
import { toast } from "react-toastify";
import { layoutsApi } from "../api/layoutsApi";
import {
  selectShareLinkExpireAfterMinutes,
  useUserPreferencesStore,
} from "../store/userPreferencesStore";
import { shareLinks } from "./shareLinks";
import { shareLinkAction } from "./shareLinkAction";

const buildShareMessage = (folderName: string, t: TFunction): string =>
  t("share.folderMessage", { ns: "files", name: folderName });

export const shareFolder = async (
  nodeId: string,
  folderName: string,
  t: TFunction,
): Promise<void> => {
  try {
    const expireAfterMinutes =
      selectShareLinkExpireAfterMinutes(useUserPreferencesStore.getState());

    const shareLink = await layoutsApi.getNodeShareLink(nodeId, expireAfterMinutes);

    const token = shareLinks.tryExtractTokenFromDownloadUrl(shareLink);
    if (!token) {
      toast.error(t("share.errors.token", { ns: "files" }), {
        toastId: `share-folder-token-${nodeId}`,
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
        toast.success(t("share.folderShared", { ns: "files", name: folderName }), {
          toastId: `share-folder-shared-${nodeId}`,
        });
        return;
      case "copied":
        toast.success(t("share.folderCopied", { ns: "files", name: folderName }), {
          toastId: `share-folder-copied-${nodeId}`,
        });
        return;
      case "aborted":
        return;
      case "error":
      default:
        toast.error(t("share.errors.copy", { ns: "files" }), {
          toastId: `share-folder-copy-${nodeId}`,
        });
    }
  } catch {
    toast.error(t("share.errors.link", { ns: "files" }), {
      toastId: `share-folder-link-${nodeId}`,
    });
  }
};
