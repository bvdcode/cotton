import * as React from "react";
import {
  Alert,
  Avatar,
  Box,
  Button,
  CircularProgress,
  Paper,
  Typography,
} from "@mui/material";
import { useTranslation } from "react-i18next";
import { useParams } from "react-router-dom";
import { toast } from "@shared/ui/notifications";
import { sharedFoldersApi } from "../../shared/api/sharedFoldersApi";
import { useCopyFeedback } from "../../shared/hooks/useCopyFeedback";
import { usePageTitle } from "../../shared/hooks/usePageTitle";
import { shareLinks } from "../../shared/utils/shareLinks";
import { shareLinkAction } from "../../shared/utils/shareLinkAction";
import { ShareFileViewer } from "./components/ShareFileViewer";
import { ShareHeaderBar } from "./components/ShareHeaderBar";
import { SharedFolderViewer } from "./components/SharedFolderViewer";
import { useShareFileInfo } from "./hooks/useShareFileInfo";
import {
  resolveSharePageViewState,
  type ShareTargetKind,
} from "./utils/sharePageViewState";

type ShareResolutionState = {
  token: string | null;
  targetKind: ShareTargetKind;
  sharedFolderInfo: {
    nodeId: string;
    name: string;
  } | null;
};

type FolderDownloadAction = {
  disabled: boolean;
  onDownload: () => void;
};

const createInitialShareResolution = (
  token: string | null,
): ShareResolutionState => ({
  token,
  targetKind: token ? "resolving" : "file",
  sharedFolderInfo: null,
});

export const SharePage: React.FC = () => {
  const { t } = useTranslation(["share", "common"]);
  const params = useParams<{ token?: string }>();
  const token = params.token ?? null;

  const [resolutionState, setResolutionState] =
    React.useState<ShareResolutionState>(() =>
      createInitialShareResolution(token),
    );
  const resolution =
    resolutionState.token === token
      ? resolutionState
      : createInitialShareResolution(token);
  const { targetKind, sharedFolderInfo } = resolution;

  React.useEffect(() => {
    if (!token) {
      return;
    }

    let cancelled = false;

    void (async () => {
      try {
        const info = await sharedFoldersApi.getInfo(token);
        if (cancelled) return;

        setResolutionState({
          token,
          sharedFolderInfo: { nodeId: info.nodeId, name: info.name },
          targetKind: "folder",
        });
      } catch {
        if (cancelled) return;

        setResolutionState({
          token,
          sharedFolderInfo: null,
          targetKind: "file",
        });
      }
    })();

    return () => {
      cancelled = true;
    };
  }, [token]);

  const inlineUrl = React.useMemo(() => {
    if (!token || targetKind !== "file") return null;
    return shareLinks.buildTokenDownloadUrl(token, "inline");
  }, [token, targetKind]);

  const downloadUrl = React.useMemo(() => {
    if (!token || targetKind !== "file") return null;
    return shareLinks.buildTokenDownloadUrl(token, "download");
  }, [token, targetKind]);

  const shareUrl = React.useMemo(() => {
    if (!token) return null;
    return shareLinks.buildShareUrl(token);
  }, [token]);

  const title = targetKind === "folder"
    ? t("folder.title", { ns: "share" })
    : t("title", { ns: "share" });
  usePageTitle(title);

  const {
    loading,
    error,
    fileName,
    contentType,
    contentLength,
    textContent,
    resolvedInlineUrl,
    encryptedContainer,
  } = useShareFileInfo({
    token: targetKind === "file" ? token : null,
    inlineUrl,
    downloadUrl,
  });

  const [isCopied, markCopied] = useCopyFeedback();
  const [folderDownloadAction, setFolderDownloadAction] =
    React.useState<FolderDownloadAction | null>(null);

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

    const resolvedName = fileName ?? sharedFolderInfo?.name ?? title;
    const shareText =
      targetKind === "folder"
        ? t("folder.message", {
            ns: "share",
            name: resolvedName,
          })
        : t("message", {
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
          return;
        case "copied":
          markCopied();
          toast.success(t("toasts.copied", { ns: "share" }), {
            toastId: "share-page-copied",
          });
          return;
        case "aborted":
          return;
        case "error":
        default:
          toast.error(t("errors.copyLink", { ns: "share" }), {
            toastId: "share-page-copy",
          });
      }
    } catch {
      toast.error(t("errors.copyLink", { ns: "share" }), {
        toastId: "share-page-copy",
      });
    }
  }, [
    fileName,
    markCopied,
    shareUrl,
    sharedFolderInfo?.name,
    t,
    targetKind,
    title,
  ]);

  const handleFolderDownload = React.useCallback(() => {
    folderDownloadAction?.onDownload();
  }, [folderDownloadAction]);

  const handleFolderDownloadActionChange = React.useCallback(
    (action: FolderDownloadAction | null) => {
      setFolderDownloadAction(action);
    },
    [],
  );

  const viewState = React.useMemo(
    () =>
      resolveSharePageViewState({
        token,
        targetKind,
        loading,
        resolvedError,
        resolvedInlineUrl,
        sharedFolderInfo,
      }),
    [
      loading,
      resolvedError,
      resolvedInlineUrl,
      sharedFolderInfo,
      targetKind,
      token,
    ],
  );

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
      {viewState.kind === "loading" && (
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

      {viewState.kind === "file-error" && (
        <Box
          flex={1}
          minHeight={0}
          display="flex"
          alignItems="center"
          justifyContent="center"
          p={2}
        >
          <Paper
            sx={{ p: 4, maxWidth: 420, width: "100%", textAlign: "center" }}
          >
            <Avatar
              src="/assets/icons/icon.svg"
              alt=""
              sx={{ width: 56, height: 56, mx: "auto", mb: 2 }}
            />
            <Alert severity="error" sx={{ mb: 3, textAlign: "left" }}>
              {viewState.message}
            </Alert>
            <Button variant="contained" href="/" fullWidth>
              {t("backButton", { ns: "notFound" })}
            </Button>
          </Paper>
        </Box>
      )}

      {viewState.kind === "folder" && (
        <>
          <ShareHeaderBar
            title={title}
            fileName={null}
            contentLength={null}
            isCopied={isCopied}
            onShareLink={handleShareLink}
            onDownload={handleFolderDownload}
            canDownload={Boolean(folderDownloadAction)}
            downloadDisabled={folderDownloadAction?.disabled ?? false}
            downloadVariant="outlined"
            shareIconOnly
          />

          <SharedFolderViewer
            token={viewState.token}
            rootNodeId={viewState.folder.nodeId}
            rootName={viewState.folder.name}
            onDownloadActionChange={handleFolderDownloadActionChange}
          />
        </>
      )}

      {viewState.kind === "file" && (
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
              token={viewState.token}
              title={title}
              inlineUrl={viewState.inlineUrl}
              downloadUrl={downloadUrl}
              fileName={fileName}
              contentType={contentType}
              contentLength={contentLength}
              textContent={textContent}
              encryptedContainer={encryptedContainer}
            />
          </Box>
        </>
      )}
    </Box>
  );
};

