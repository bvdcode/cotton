import * as React from "react";
import {
  Alert,
  Box,
  CircularProgress,
  Snackbar,
  Typography,
} from "@mui/material";
import { useTranslation } from "react-i18next";
import { useParams } from "react-router-dom";
import { useCopyFeedback } from "../../shared/hooks/useCopyFeedback";
import { shareLinks } from "../../shared/utils/shareLinks";
import { shareLinkAction } from "../../shared/utils/shareLinkAction";
import { ShareFileViewer } from "./components/ShareFileViewer";
import { ShareHeaderBar } from "./components/ShareHeaderBar";
import { useShareFileInfo } from "./hooks/useShareFileInfo";

export const SharePage: React.FC = () => {
  const { t } = useTranslation(["share", "common"]);
  const params = useParams<{ token?: string }>();
  const token = params.token ?? null;

  const inlineUrl = React.useMemo(() => {
    if (!token) return null;
    return shareLinks.buildTokenDownloadUrl(token, "inline");
  }, [token]);

  const downloadUrl = React.useMemo(() => {
    if (!token) return null;
    return shareLinks.buildTokenDownloadUrl(token, "download");
  }, [token]);

  const shareUrl = React.useMemo(() => {
    if (!token) return null;
    return shareLinks.buildShareUrl(token);
  }, [token]);

  const title = t("title", { ns: "share" });

  const {
    loading,
    error,
    fileName,
    contentType,
    contentLength,
    textContent,
    resolvedInlineUrl,
  } = useShareFileInfo({ token, inlineUrl, downloadUrl });

  const [shareToast, setShareToast] = React.useState<{
    open: boolean;
    message: string;
  }>({ open: false, message: "" });

  const [isCopied, markCopied] = useCopyFeedback();

  const resolvedError = React.useMemo((): string | null => {
    if (!error) return null;
    switch (error) {
      case "invalidLink":
        return t("errors.invalidLink", { ns: "share" });
      case "notFound":
        return t("errors.notFound", { ns: "share" });
      case "loadFailed":
      default:
        return t("errors.loadFailed", { ns: "share" });
    }
  }, [error, t]);

  const handleDownload = React.useCallback(() => {
    if (!downloadUrl) return;
    window.location.href = downloadUrl;
  }, [downloadUrl]);

  const handleShareLink = React.useCallback(async () => {
    if (!shareUrl) return;

    const resolvedName = fileName ?? title;
    const shareText = t("message", {
      ns: "share",
      name: resolvedName,
    });

    try {
      const outcome = await shareLinkAction({
        title: resolvedName,
        text: shareText,
        url: shareUrl,
      });

      switch (outcome.kind) {
        case "shared":
          setShareToast({
            open: true,
            message: t("toasts.shared", { ns: "share" }),
          });
          return;
        case "copied":
          markCopied();
          setShareToast({
            open: true,
            message: t("toasts.copied", { ns: "share" }),
          });
          return;
        case "aborted":
          return;
        case "error":
        default:
          setShareToast({
            open: true,
            message: t("errors.copyLink", { ns: "share" }),
          });
      }
    } catch {
      setShareToast({
        open: true,
        message: t("errors.copyLink", { ns: "share" }),
      });
    }
  }, [fileName, markCopied, shareUrl, t, title]);

  return (
    <Box
      width="100%"
      height="100%"
      alignSelf="stretch"
      display="flex"
      flexDirection="column"
      flex={1}
      minHeight={0}
      minWidth={0}
    >
      <Snackbar
        open={shareToast.open}
        autoHideDuration={2500}
        onClose={() => setShareToast((prev) => ({ ...prev, open: false }))}
        message={shareToast.message}
      />

      {loading && (
        <Box
          flex={1}
          minHeight={0}
          display="flex"
          alignItems="center"
          justifyContent="center"
          gap={2}
        >
          <CircularProgress size={20} />
          <Typography color="text.secondary">
            {t("loading", { ns: "share" })}
          </Typography>
        </Box>
      )}

      {!loading && resolvedError && (
        <Box
          flex={1}
          minHeight={0}
          display="flex"
          alignItems="center"
          justifyContent="center"
          p={2}
        >
          <Alert severity="error">{resolvedError}</Alert>
        </Box>
      )}

      {!loading && !resolvedError && token && resolvedInlineUrl && (
        <>
          <ShareHeaderBar
            title={title}
            fileName={fileName}
            contentLength={contentLength}
            isCopied={isCopied}
            onShareLink={handleShareLink}
            onDownload={handleDownload}
            canDownload={Boolean(downloadUrl)}
          />

          <Box
            flex={1}
            minHeight={0}
            display="flex"
            alignItems="center"
            justifyContent="center"
            overflow="hidden"
          >
            <ShareFileViewer
              token={token}
              title={title}
              inlineUrl={resolvedInlineUrl}
              downloadUrl={downloadUrl}
              fileName={fileName}
              contentType={contentType}
              contentLength={contentLength}
              textContent={textContent}
            />
          </Box>
        </>
      )}
    </Box>
  );
};

