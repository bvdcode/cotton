import * as React from "react";
import {
  Avatar,
  Box,
  Button,
  IconButton,
  Tooltip,
  Typography,
} from "@mui/material";
import { Check, Download, Share as ShareIcon } from "@mui/icons-material";
import { useTranslation } from "react-i18next";
import { formatBytes } from "../../../shared/utils/formatBytes";

interface ShareHeaderBarProps {
  title: string;
  fileName: string | null;
  contentLength: number | null;
  isCopied: boolean;
  onShareLink: () => void;
  onDownload: () => void;
  canDownload: boolean;
  downloadDisabled?: boolean;
  downloadVariant?: "contained" | "outlined" | "text";
  shareIconOnly?: boolean;
}

export const ShareHeaderBar: React.FC<ShareHeaderBarProps> = ({
  title,
  fileName,
  contentLength,
  isCopied,
  onShareLink,
  onDownload,
  canDownload,
  downloadDisabled = false,
  downloadVariant = "contained",
  shareIconOnly = false,
}) => {
  const { t } = useTranslation(["share", "common"]);
  const shareTitle = isCopied
    ? t("actions.copied", { ns: "common" })
    : t("actions.share", { ns: "common" });

  return (
    <Box
      position="sticky"
      top={0}
      zIndex={1}
      bgcolor="background.default"
      display="flex"
      alignItems="center"
      justifyContent="space-between"
      gap={2}
      px={{ xs: 2, sm: 3 }}
      py={0.75}
      minWidth={0}
      borderBottom={1}
      borderColor="divider"
      sx={{ minHeight: 48 }}
    >
      <Box
        display="flex"
        alignItems="center"
        gap={1}
        minWidth={0}
        flex={1}
        overflow="hidden"
      >
        <Avatar
          src="/assets/icons/icon.svg"
          alt={t("app.logoAlt", { ns: "common" })}
          sx={{ width: 26, height: 26 }}
        />
        <Typography variant="subtitle1" noWrap sx={{ minWidth: 0 }}>
          {fileName ?? title}
          {contentLength !== null && (
            <Box component="span" sx={{ color: "text.secondary", ml: 1 }}>
              • {formatBytes(contentLength)}
            </Box>
          )}
        </Typography>
      </Box>

      <Box display="flex" alignItems="center" gap={1} flexShrink={0}>
        {shareIconOnly ? (
          <Tooltip title={shareTitle} disableInteractive>
            <IconButton
              aria-label={shareTitle}
              onClick={onShareLink}
              size="small"
              color={isCopied ? "success" : "primary"}
            >
              {isCopied ? <Check /> : <ShareIcon />}
            </IconButton>
          </Tooltip>
        ) : (
          <Button
            onClick={onShareLink}
            variant="outlined"
            startIcon={isCopied ? <Check /> : <ShareIcon />}
            size="small"
            color={isCopied ? "success" : "primary"}
          >
            {shareTitle}
          </Button>
        )}

        {canDownload && (
          <Button
            onClick={onDownload}
            variant={downloadVariant}
            startIcon={<Download />}
            size="small"
            disabled={downloadDisabled}
          >
            {t("actions.download", { ns: "common" })}
          </Button>
        )}
      </Box>
    </Box>
  );
};
