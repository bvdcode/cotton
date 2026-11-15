import { useState } from "react";
import { useTranslation } from "react-i18next";
import { filesApi } from "@shared/api/filesApi";
import { isAxiosError } from "@shared/api/httpClient";
import { layoutsApi } from "@shared/api/layoutsApi";
import {
  selectShareLinkExpireAfterMinutes,
  useUserPreferencesStore,
} from "@shared/store/userPreferencesStore";
import { shareLinks } from "@shared/utils/shareLinks";
import {
  getShareLinkDuration,
  getShareLinkExpirationMinutes,
  isValidShareLinkName,
  type ShareLinkDurationUnit,
  type ShareLinkTarget,
} from "@shared/utils/shareLinkOptions";
import { useCopyFeedback } from "./useCopyFeedback";

export const useShareLinkForm = (target: ShareLinkTarget) => {
  const { t } = useTranslation(["files", "common"]);
  const [initialDuration] = useState(() =>
    getShareLinkDuration(
      selectShareLinkExpireAfterMinutes(useUserPreferencesStore.getState()),
    ),
  );
  const [allowUpload, setAllowUpload] = useState(false);
  const [hideFolderContent, setHideFolderContent] = useState(false);
  const [name, setName] = useState("");
  const [duration, setDuration] = useState(initialDuration.value);
  const [unit, setUnit] = useState<ShareLinkDurationUnit>(initialDuration.unit);
  const [pending, setPending] = useState(false);
  const [link, setLink] = useState("");
  const [error, setError] = useState("");
  const [copied, markCopied] = useCopyFeedback();
  const minutes = getShareLinkExpirationMinutes(duration, unit);
  const validName = isValidShareLinkName(name);
  const locked = pending || link.length > 0;

  const createLink = async () => {
    if (minutes === undefined || !validName || locked) {
      return;
    }
    setPending(true);
    setError("");
    try {
      let result: string;
      switch (target.kind) {
        case "file":
          result = await filesApi.getShareLink(target.id, minutes, name.trim());
          break;
        case "folder":
          result = await layoutsApi.getNodeShareLink(
            target.id,
            minutes,
            name.trim(),
            allowUpload,
            hideFolderContent,
          );
          break;
      }
      const token = shareLinks.tryExtractTokenFromDownloadUrl(result);
      if (!token) {
        setError(t("share.errors.token"));
        return;
      }
      setLink(shareLinks.buildShareUrl(token));
    } catch (failure) {
      setError(
        isAxiosError(failure) && failure.response?.status === 409
          ? t("share.options.nameTaken")
          : t("share.errors.link"),
      );
    } finally {
      setPending(false);
    }
  };

  const copyLink = async () => {
    try {
      await navigator.clipboard.writeText(link);
      markCopied();
      setError("");
    } catch {
      setError(t("share.errors.copy"));
    }
  };
  const copyLabel = copied
    ? t("common:actions.copied")
    : t("common:actions.copy");

  return {
    allowUpload,
    setAllowUpload,
    hideFolderContent,
    setHideFolderContent,
    name,
    setName,
    duration,
    setDuration,
    unit,
    setUnit,
    pending,
    link,
    error,
    setError,
    copied,
    copyLabel,
    minutes,
    validName,
    locked,
    createLink,
    copyLink,
  };
};
