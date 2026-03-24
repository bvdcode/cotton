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

const SKELETON_COUNT = 5;
const MAX_VISIBLE_FILES = 9;

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
  const visibleFiles = React.useMemo(
    () => files.slice(0, MAX_VISIBLE_FILES),
    [files],
  );

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
    <Card sx={{ gridColumn: "1 / -1", minWidth: 0 }}>
      <CardContent>
        <Typography variant="overline" color="text.secondary">
          {t("cards.recentFiles.title")}
        </Typography>

        {loading && visibleFiles.length === 0 ? (
          <Box
            sx={{
              display: "grid",
              gridTemplateColumns: {
                xs: "1fr",
                sm: "repeat(2, minmax(0, 1fr))",
                md: "none",
              },
              gridTemplateRows: { md: "repeat(2, minmax(0, 1fr))" },
              gridAutoFlow: { md: "column" },
              gridAutoColumns: { md: "minmax(260px, 1fr)" },
              gap: 1,
              mt: 1,
              minWidth: 0,
              overflowX: { md: "auto" },
              overflowY: "hidden",
              pb: { md: 0.5 },
            }}
          >
            {Array.from({ length: SKELETON_COUNT }, (_, i) => (
              <Skeleton key={i} variant="rounded" height={48} />
            ))}
          </Box>
        ) : visibleFiles.length === 0 ? (
          <Typography variant="body2" color="text.secondary" mt={1}>
            {t("cards.recentFiles.empty")}
          </Typography>
        ) : (
          <Box
            sx={{
              display: "grid",
              gridTemplateColumns: {
                xs: "1fr",
                sm: "repeat(2, minmax(0, 1fr))",
                md: "none",
              },
              gridTemplateRows: { md: "repeat(2, minmax(0, 1fr))" },
              gridAutoFlow: { md: "column" },
              gridAutoColumns: { md: "minmax(280px, 1fr)" },
              gap: 0.75,
              mt: 1,
              minWidth: 0,
              overflowX: { md: "auto" },
              overflowY: "hidden",
              pb: { md: 0.5 },
            }}
          >
            {visibleFiles.map((file) => (
              <CardActionArea
                key={file.id}
                onClick={() => handleFileClick(file)}
                sx={{ borderRadius: 1, minWidth: 0, width: "100%" }}
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
