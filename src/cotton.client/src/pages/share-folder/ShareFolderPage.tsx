import * as React from "react";
import {
  Alert,
  Box,
  Breadcrumbs,
  CircularProgress,
  IconButton,
  Link,
  Snackbar,
  Typography,
} from "@mui/material";
import {
  ArrowBack,
  ContentCopy,
  Download,
  Folder,
  InsertDriveFile,
  Share,
} from "@mui/icons-material";
import { useTranslation } from "react-i18next";
import { useParams } from "react-router-dom";
import {
  sharedFoldersApi,
  type SharedFolderInfoDto,
} from "../../shared/api/sharedFoldersApi";
import type { NodeDto } from "../../shared/api/layoutsApi";
import type {
  NodeContentDto,
  NodeFileManifestDto,
} from "../../shared/api/nodesApi";
import { shareLinks } from "../../shared/utils/shareLinks";
import { shareLinkAction } from "../../shared/utils/shareLinkAction";
import { useCopyFeedback } from "../../shared/hooks/useCopyFeedback";
import { formatBytes } from "../../shared/utils/formatBytes";

interface BrowseState {
  currentNodeId: string | null;
  ancestors: NodeDto[];
  content: NodeContentDto | null;
  loading: boolean;
}

export const ShareFolderPage: React.FC = () => {
  const { t } = useTranslation(["share", "common"]);
  const params = useParams<{ token?: string }>();
  const token = params.token ?? null;

  const [folderInfo, setFolderInfo] =
    React.useState<SharedFolderInfoDto | null>(null);
  const [error, setError] = React.useState<string | null>(null);
  const [infoLoading, setInfoLoading] = React.useState(true);

  const [browse, setBrowse] = React.useState<BrowseState>({
    currentNodeId: null,
    ancestors: [],
    content: null,
    loading: false,
  });

  const [shareToast, setShareToast] = React.useState<{
    open: boolean;
    message: string;
  }>({ open: false, message: "" });

  const [isCopied, markCopied] = useCopyFeedback();

  React.useEffect(() => {
    if (!token) {
      setError(t("errors.invalidLink", { ns: "share" }));
      setInfoLoading(false);
      return;
    }

    let cancelled = false;

    const load = async () => {
      try {
        const info = await sharedFoldersApi.getInfo(token);
        if (cancelled) return;
        setFolderInfo(info);
        setInfoLoading(false);

        setBrowse((prev) => ({ ...prev, loading: true }));
        const children = await sharedFoldersApi.getChildren(token);
        if (cancelled) return;
        setBrowse({
          currentNodeId: info.nodeId,
          ancestors: [],
          content: children.content,
          loading: false,
        });
      } catch {
        if (cancelled) return;
        setError(t("errors.notFound", { ns: "share" }));
        setInfoLoading(false);
      }
    };

    void load();
    return () => {
      cancelled = true;
    };
  }, [token, t]);

  const navigateToNode = React.useCallback(
    async (nodeId: string) => {
      if (!token || !folderInfo) return;

      setBrowse((prev) => ({ ...prev, loading: true }));
      try {
        const [childrenResult, ancestorsResult] = await Promise.all([
          nodeId === folderInfo.nodeId
            ? sharedFoldersApi.getChildren(token)
            : sharedFoldersApi.getSubfolderChildren(token, nodeId),
          nodeId === folderInfo.nodeId
            ? Promise.resolve([])
            : sharedFoldersApi.getAncestors(token, nodeId),
        ]);

        setBrowse({
          currentNodeId: nodeId,
          ancestors: ancestorsResult,
          content: childrenResult.content,
          loading: false,
        });
      } catch {
        setBrowse((prev) => ({ ...prev, loading: false }));
      }
    },
    [token, folderInfo],
  );

  const handleFolderClick = React.useCallback(
    (nodeId: string) => {
      void navigateToNode(nodeId);
    },
    [navigateToNode],
  );

  const handleGoUp = React.useCallback(() => {
    if (!folderInfo) return;

    if (browse.ancestors.length > 0) {
      const parent = browse.ancestors[browse.ancestors.length - 1];
      void navigateToNode(parent.id);
    } else {
      void navigateToNode(folderInfo.nodeId);
    }
  }, [browse.ancestors, folderInfo, navigateToNode]);

  const handleFileDownload = React.useCallback(
    (nodeFileId: string) => {
      if (!token) return;
      const url = sharedFoldersApi.buildFileDownloadUrl(token, nodeFileId);
      window.location.href = url;
    },
    [token],
  );

  const shareUrl = React.useMemo(() => {
    if (!token) return null;
    return shareLinks.buildShareUrl(token);
  }, [token]);

  const handleShareLink = React.useCallback(async () => {
    if (!shareUrl || !folderInfo) return;

    const outcome = await shareLinkAction({
      title: folderInfo.name,
      text: t("folderMessage", { ns: "share", name: folderInfo.name }),
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
  }, [shareUrl, folderInfo, markCopied, t]);

  const isAtRoot =
    folderInfo !== null && browse.currentNodeId === folderInfo.nodeId;

  if (infoLoading) {
    return (
      <Box
        display="flex"
        alignItems="center"
        justifyContent="center"
        flex={1}
        gap={2}
      >
        <CircularProgress size={20} />
        <Typography color="text.secondary">
          {t("loading", { ns: "share" })}
        </Typography>
      </Box>
    );
  }

  if (error) {
    return (
      <Box
        display="flex"
        alignItems="center"
        justifyContent="center"
        flex={1}
        p={2}
      >
        <Alert severity="error">{error}</Alert>
      </Box>
    );
  }

  return (
    <Box
      width="100%"
      height="100%"
      display="flex"
      flexDirection="column"
      flex={1}
      minHeight={0}
    >
      <Snackbar
        open={shareToast.open}
        autoHideDuration={2500}
        onClose={() => setShareToast((prev) => ({ ...prev, open: false }))}
        message={shareToast.message}
      />

      <SharedFolderHeader
        folderName={folderInfo?.name ?? ""}
        isAtRoot={isAtRoot}
        ancestors={browse.ancestors}
        isCopied={isCopied}
        onGoUp={handleGoUp}
        onNavigate={handleFolderClick}
        onGoToRoot={() => folderInfo && handleFolderClick(folderInfo.nodeId)}
        onShareLink={handleShareLink}
      />

      <Box flex={1} minHeight={0} overflow="auto" p={2}>
        {browse.loading ? (
          <Box
            display="flex"
            alignItems="center"
            justifyContent="center"
            py={4}
          >
            <CircularProgress size={20} />
          </Box>
        ) : (
          <SharedFolderContent
            content={browse.content}
            onFolderClick={handleFolderClick}
            onFileDownload={handleFileDownload}
          />
        )}
      </Box>
    </Box>
  );
};

interface SharedFolderHeaderProps {
  folderName: string;
  isAtRoot: boolean;
  ancestors: NodeDto[];
  isCopied: boolean;
  onGoUp: () => void;
  onNavigate: (nodeId: string) => void;
  onGoToRoot: () => void;
  onShareLink: () => void;
}

const SharedFolderHeader: React.FC<SharedFolderHeaderProps> = ({
  folderName,
  isAtRoot,
  ancestors,
  isCopied,
  onGoUp,
  onNavigate,
  onGoToRoot,
  onShareLink,
}) => {
  const { t } = useTranslation(["share", "common"]);

  return (
    <Box
      display="flex"
      alignItems="center"
      gap={1}
      px={2}
      py={1}
      borderBottom={1}
      borderColor="divider"
    >
      {!isAtRoot && (
        <IconButton size="small" onClick={onGoUp}>
          <ArrowBack fontSize="small" />
        </IconButton>
      )}

      <Folder color="primary" />

      <Breadcrumbs maxItems={5} sx={{ flex: 1 }}>
        <Link
          component="button"
          underline="hover"
          color="inherit"
          onClick={onGoToRoot}
        >
          {folderName}
        </Link>
        {ancestors.map((ancestor) => (
          <Link
            key={ancestor.id}
            component="button"
            underline="hover"
            color="inherit"
            onClick={() => onNavigate(ancestor.id)}
          >
            {ancestor.name}
          </Link>
        ))}
      </Breadcrumbs>

      <IconButton
        size="small"
        onClick={onShareLink}
        title={t("common:actions.share")}
      >
        {isCopied ? (
          <ContentCopy fontSize="small" />
        ) : (
          <Share fontSize="small" />
        )}
      </IconButton>
    </Box>
  );
};

interface SharedFolderContentProps {
  content: NodeContentDto | null;
  onFolderClick: (nodeId: string) => void;
  onFileDownload: (nodeFileId: string) => void;
}

const SharedFolderContent: React.FC<SharedFolderContentProps> = ({
  content,
  onFolderClick,
  onFileDownload,
}) => {
  const { t } = useTranslation(["share", "common"]);

  if (!content) return null;

  const hasNodes = content.nodes.length > 0;
  const hasFiles = content.files.length > 0;

  if (!hasNodes && !hasFiles) {
    return (
      <Box
        display="flex"
        alignItems="center"
        justifyContent="center"
        py={4}
      >
        <Typography color="text.secondary">
          {t("folderEmpty", { ns: "share" })}
        </Typography>
      </Box>
    );
  }

  return (
    <Box display="flex" flexDirection="column" gap={0.5}>
      {content.nodes.map((node) => (
        <SharedFolderItem
          key={node.id}
          icon={<Folder color="primary" />}
          name={node.name}
          onClick={() => onFolderClick(node.id)}
        />
      ))}
      {content.files.map((file) => (
        <SharedFileItem
          key={file.id}
          file={file}
          onDownload={() => onFileDownload(file.id)}
        />
      ))}
    </Box>
  );
};

interface SharedFolderItemProps {
  icon: React.ReactNode;
  name: string;
  subtitle?: string;
  onClick: () => void;
  action?: React.ReactNode;
}

const SharedFolderItem: React.FC<SharedFolderItemProps> = ({
  icon,
  name,
  subtitle,
  onClick,
  action,
}) => (
  <Box
    display="flex"
    alignItems="center"
    gap={1.5}
    px={1.5}
    py={1}
    borderRadius={1}
    sx={{
      cursor: "pointer",
      "&:hover": { bgcolor: "action.hover" },
      transition: "background-color 0.15s",
    }}
    onClick={onClick}
  >
    {icon}
    <Box flex={1} minWidth={0}>
      <Typography noWrap variant="body2">
        {name}
      </Typography>
      {subtitle && (
        <Typography noWrap variant="caption" color="text.secondary">
          {subtitle}
        </Typography>
      )}
    </Box>
    {action}
  </Box>
);

interface SharedFileItemProps {
  file: NodeFileManifestDto;
  onDownload: () => void;
}

const SharedFileItem: React.FC<SharedFileItemProps> = ({
  file,
  onDownload,
}) => {
  const { t } = useTranslation(["common"]);

  return (
    <SharedFolderItem
      icon={<InsertDriveFile color="action" />}
      name={file.name}
      subtitle={formatBytes(file.sizeBytes)}
      onClick={onDownload}
      action={
        <IconButton
          size="small"
          onClick={(e) => {
            e.stopPropagation();
            onDownload();
          }}
          title={t("common:actions.download")}
        >
          <Download fontSize="small" />
        </IconButton>
      }
    />
  );
};
