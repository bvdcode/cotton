import React from "react";
import {
  Card,
  CardContent,
  Typography,
  Box,
  Skeleton,
  CardActionArea,
} from "@mui/material";
import { useTranslation } from "react-i18next";
import { useNavigate } from "react-router-dom";
import type { NodeFileManifestDto } from "../../../shared/api/nodesApi";
import { RecentFileItem } from "./RecentFileItem";

interface RecentFilesCardProps {
  files: NodeFileManifestDto[];
  loading: boolean;
}

export const RecentFilesCard: React.FC<RecentFilesCardProps> = ({
  files,
  loading,
}) => {
  const { t } = useTranslation(["home", "common"]);
  const navigate = useNavigate();

  const handleFileClick = React.useCallback(
    (file: NodeFileManifestDto) => {
      if (!file.nodeId) return;
      navigate(`/files/${file.nodeId}`, {
        state: { selectedFileId: file.id },
      });
    },
    [navigate],
  );

  return (
    <Card sx={{ gridColumn: { xs: "1", md: "span 4" } }}>
      <CardContent>
        <Typography variant="overline" color="text.secondary">
          {t("cards.recentFiles.title")}
        </Typography>

        {loading && files.length === 0 ? (
          <Box display="flex" flexDirection="column" gap={1} mt={1}>
            {Array.from({ length: 3 }, (_, i) => (
              <Skeleton key={i} variant="rounded" height={48} />
            ))}
          </Box>
        ) : files.length === 0 ? (
          <Typography variant="body2" color="text.secondary" mt={1}>
            {t("cards.recentFiles.empty")}
          </Typography>
        ) : (
          <Box display="flex" flexDirection="column" gap={0.5} mt={1}>
            {files.map((file) => (
              <CardActionArea
                key={file.id}
                onClick={() => handleFileClick(file)}
                sx={{ borderRadius: 1 }}
              >
                <RecentFileItem file={file} t={t} />
              </CardActionArea>
            ))}
          </Box>
        )}
      </CardContent>
    </Card>
  );
};
