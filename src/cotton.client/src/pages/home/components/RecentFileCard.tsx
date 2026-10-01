import React from "react";
import {
  Box,
  CardActionArea,
  IconButton,
  Tooltip,
  Typography,
} from "@mui/material";
import { OpenInNew } from "@mui/icons-material";
import { useTranslation } from "react-i18next";
import type { NodeFileManifestDto } from "../../../shared/api/nodesApi";
import { getFileIcon } from "@shared/utils/icons";
import { formatBytes } from "../../../shared/utils/formatBytes";
import { formatTimeAgo } from "../../../shared/utils/formatTimeAgo";

interface RecentFileCardProps {
  file: NodeFileManifestDto;
  onClick: () => void;
  onOpenLocation: () => void;
}

const PREVIEW_SIZE = 40;
const PREVIEW_ICON_SIZE = 28;

export const RecentFileCard: React.FC<RecentFileCardProps> = ({
  file,
  onClick,
  onOpenLocation,
}) => {
  const { t } = useTranslation(["common", "files"]);
  const icon = React.useMemo(
    () =>
      getFileIcon(
        file.previewHashEncryptedHex ?? null,
        file.name,
        file.contentType,
        {
          hideExtensionLabel: true,
        },
      ),
    [file.previewHashEncryptedHex, file.name, file.contentType],
  );

  const isPreviewUrl = typeof icon === "string";

  return (
    <Box
      display="flex"
      alignItems="center"
      sx={{
        border: "1px solid",
        borderColor: "divider",
        borderRadius: 1,
        height: "100%",
        minWidth: 0,
        width: "100%",
      }}
    >
      <CardActionArea onClick={onClick} sx={{ flex: 1, minWidth: 0 }}>
        <Box display="flex" alignItems="center" gap={1.5} px={1} py={0.75}>
          <Box
            width={PREVIEW_SIZE}
            height={PREVIEW_SIZE}
            flexShrink={0}
            display="flex"
            alignItems="center"
            justifyContent="center"
            overflow="hidden"
            borderRadius={1}
            sx={{ "& > svg": { fontSize: PREVIEW_ICON_SIZE } }}
          >
            {isPreviewUrl ? (
              <Box
                component="img"
                src={icon}
                alt=""
                width={PREVIEW_SIZE}
                height={PREVIEW_SIZE}
                sx={{ objectFit: "cover" }}
              />
            ) : (
              icon
            )}
          </Box>

          <Box minWidth={0} flex={1}>
            <Typography variant="body2" noWrap>
              {file.name}
            </Typography>
            <Typography variant="caption" color="text.secondary" noWrap>
              {formatBytes(file.sizeBytes)} &middot;{" "}
              {formatTimeAgo(file.createdAt, t)}
            </Typography>
          </Box>
        </Box>
      </CardActionArea>
      <Tooltip title={t("files:actions.goToFolder")}>
        <IconButton
          aria-label={t("files:actions.goToFolder")}
          onClick={onOpenLocation}
        >
          <OpenInNew />
        </IconButton>
      </Tooltip>
    </Box>
  );
};
