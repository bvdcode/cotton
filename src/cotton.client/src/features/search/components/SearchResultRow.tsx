import { Box, ButtonBase, IconButton, Stack, Tooltip, Typography } from "@mui/material";
import {
  Download,
  Folder,
  FolderOpen,
  OpenInNew,
  Settings,
  Share,
} from "@mui/icons-material";
import { memo } from "react";
import { useTranslation } from "react-i18next";
import { formatBytes } from "../../../shared/utils/formatBytes";
import { getFileTypeInfo } from "@shared/utils/fileTypes";
import { getSmallFileIcon } from "../utils/fileIcon";
import type { SearchRow } from "../types";

interface SearchResultRowProps {
  row: SearchRow;
  isLast: boolean;
  previewFailed: boolean;
  onPreviewError: (fileId: string) => void;
  onActivate: (row: SearchRow) => void;
  onShareFile: (row: Extract<SearchRow, { kind: "file" }>) => void;
  onOpenFileFolder: (row: Extract<SearchRow, { kind: "file" }>) => void;
  onDownloadFile: (row: Extract<SearchRow, { kind: "file" }>) => void;
}

const renderPreview = (
  row: SearchRow,
  previewFailed: boolean,
  onPreviewError: (fileId: string) => void,
) => {
  if (row.kind === "setting") {
    return <Settings color="primary" sx={{ fontSize: 28 }} />;
  }

  if (row.kind === "folder") {
    return <Folder color="primary" sx={{ fontSize: 30 }} />;
  }

  const previewHash = row.file.previewHashEncryptedHex;
  const previewUrl =
    previewHash && !previewFailed
      ? `/api/v1/preview/${encodeURIComponent(previewHash)}.webp`
      : null;

  if (previewUrl) {
    return (
      <Box
        component="img"
        src={previewUrl}
        alt=""
        loading="lazy"
        onError={() => onPreviewError(row.file.id)}
        sx={{
          width: "100%",
          height: "100%",
          objectFit: "cover",
          borderRadius: 1,
        }}
      />
    );
  }

  return getSmallFileIcon(row.file.name);
};

const SearchResultRowImpl = ({
  row,
  isLast,
  previewFailed,
  onPreviewError,
  onActivate,
  onShareFile,
  onOpenFileFolder,
  onDownloadFile,
}: SearchResultRowProps) => {
  const { t } = useTranslation("search");

  const text = (() => {
    if (row.kind === "setting") {
      return {
        title: row.entry.title,
        meta: row.entry.description ?? t("types.setting"),
        action: t("actions.openSetting"),
      };
    }

    if (row.kind === "folder") {
      return {
        title: row.node.name,
        meta: row.path ?? t("types.folder"),
        action: t("actions.openFolder"),
      };
    }

    const size = formatBytes(row.file.sizeBytes);
    return {
      title: row.file.name,
      meta: row.path ? `${row.path} - ${size}` : size,
      action: t("actions.openFile"),
    };
  })();

  const fileTypeInfo =
    row.kind === "file"
      ? getFileTypeInfo(row.file.name, row.file.contentType)
      : null;
  const isDownloadOnly =
    row.kind === "file" && fileTypeInfo?.supportsInlineView === false;
  const primaryAction = isDownloadOnly ? t("actions.downloadFile") : text.action;

  const handlePrimaryAction = (event: React.MouseEvent) => {
    event.stopPropagation();
    if (row.kind === "file" && isDownloadOnly) {
      onDownloadFile(row);
      return;
    }
    onActivate(row);
  };

  return (
    <ButtonBase
      onClick={() => onActivate(row)}
      sx={{
        width: "100%",
        minHeight: 68,
        justifyContent: "stretch",
        textAlign: "left",
        px: 1.25,
        py: 1,
        borderBottom: isLast ? 0 : 1,
        borderColor: "divider",
        bgcolor: "background.default",
        "&:hover": {
          bgcolor: "action.hover",
        },
      }}
    >
      <Stack
        direction="row"
        spacing={1.25}
        alignItems="center"
        width="100%"
        minWidth={0}
      >
        <Box
          sx={{
            width: 44,
            height: 44,
            flexShrink: 0,
            borderRadius: 1,
            bgcolor: "action.hover",
            display: "flex",
            alignItems: "center",
            justifyContent: "center",
            overflow: "hidden",
          }}
        >
          {renderPreview(row, previewFailed, onPreviewError)}
        </Box>

        <Stack spacing={0.25} minWidth={0} flex={1}>
          <Typography
            variant="body2"
            fontWeight={700}
            noWrap
            title={text.title}
          >
            {text.title}
          </Typography>
          <Typography
            variant="caption"
            color="text.secondary"
            noWrap
            title={text.meta}
          >
            {text.meta}
          </Typography>
        </Stack>

        {row.kind === "file" && (
          <>
            {!isDownloadOnly && (
              <Tooltip title={t("actions.downloadFile")}>
                <IconButton
                  size="small"
                  aria-label={t("actions.downloadFile")}
                  onClick={(event) => {
                    event.stopPropagation();
                    onDownloadFile(row);
                  }}
                  sx={{ flexShrink: 0 }}
                >
                  <Download fontSize="small" />
                </IconButton>
              </Tooltip>
            )}
            <Tooltip title={t("actions.shareFile")}>
              <IconButton
                size="small"
                aria-label={t("actions.shareFile")}
                onClick={(event) => {
                  event.stopPropagation();
                  onShareFile(row);
                }}
                sx={{ flexShrink: 0 }}
              >
                <Share fontSize="small" />
              </IconButton>
            </Tooltip>
            <Tooltip title={t("actions.openContainingFolder")}>
              <IconButton
                size="small"
                aria-label={t("actions.openContainingFolder")}
                onClick={(event) => {
                  event.stopPropagation();
                  onOpenFileFolder(row);
                }}
                sx={{ flexShrink: 0 }}
              >
                <FolderOpen fontSize="small" />
              </IconButton>
            </Tooltip>
          </>
        )}

        <Tooltip title={primaryAction}>
          <IconButton
            size="small"
            aria-label={primaryAction}
            onClick={handlePrimaryAction}
            sx={{ flexShrink: 0 }}
          >
            {row.kind === "folder" ? (
              <FolderOpen fontSize="small" />
            ) : isDownloadOnly ? (
              <Download fontSize="small" />
            ) : (
              <OpenInNew fontSize="small" />
            )}
          </IconButton>
        </Tooltip>
      </Stack>
    </ButtonBase>
  );
};

export const SearchResultRow = memo(SearchResultRowImpl);
