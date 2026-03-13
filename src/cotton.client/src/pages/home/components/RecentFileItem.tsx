import React from "react";
import { Box, Typography } from "@mui/material";
import type { TFunction } from "i18next";
import type { NodeFileManifestDto } from "../../../shared/api/nodesApi";
import { getFileIcon } from "../../files/utils/icons";
import { formatBytes } from "../../../shared/utils/formatBytes";
import { formatTimeAgo } from "../../../shared/utils/formatTimeAgo";

interface RecentFileItemProps {
  file: NodeFileManifestDto;
  t: TFunction;
}

const PREVIEW_SIZE = 40;

export const RecentFileItem: React.FC<RecentFileItemProps> = ({ file, t }) => {
  const icon = React.useMemo(
    () =>
      getFileIcon(
        file.previewHashEncryptedHex ?? null,
        file.name,
        file.contentType,
      ),
    [file.previewHashEncryptedHex, file.name, file.contentType],
  );

  const isPreviewUrl = typeof icon === "string";

  return (
    <Box
      display="flex"
      alignItems="center"
      gap={1.5}
      px={1}
      py={0.75}
    >
      <Box
        width={PREVIEW_SIZE}
        height={PREVIEW_SIZE}
        flexShrink={0}
        display="flex"
        alignItems="center"
        justifyContent="center"
        overflow="hidden"
        borderRadius={1}
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
  );
};
