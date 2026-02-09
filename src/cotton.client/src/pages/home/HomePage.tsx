import { useEffect } from "react";
import { Box, Card, CardContent, Typography, Alert } from "@mui/material";
import { useTranslation } from "react-i18next";
import Loader from "../../shared/ui/Loader";
import { useLayoutsStore } from "../../shared/store/layoutsStore";
import { formatBytes } from "../../shared/utils/formatBytes";

export const HomePage: React.FC = () => {
  const { t } = useTranslation(["home", "common"]);
  const {
    rootNode,
    statsByLayoutId,
    loadingRoot,
    loadingStats,
    error,
    ensureHomeData,
  } = useLayoutsStore();

  const layoutId = rootNode?.layoutId;
  const stats = layoutId ? statsByLayoutId[layoutId] : undefined;

  useEffect(() => {
    void ensureHomeData();
  }, [ensureHomeData]);

  const isLoading = loadingRoot || loadingStats;

  if (isLoading && !rootNode && !stats) {
    return <Loader title={t("loading.title")} caption={t("loading.caption")} />;
  }

  return (
    <Box py={3} width="100%">
      {error && (
        <Box mb={2}>
          <Alert severity="error">{error}</Alert>
        </Box>
      )}

      <Box
        sx={{
          display: "grid",
          gap: 2,
          gridTemplateColumns: {
            xs: "1fr",
            md: "repeat(4, 1fr)",
          },
        }}
      >
        <Card>
          <CardContent>
            <Typography variant="overline" color="text.secondary">
              {t("cards.folders.layoutTitle")}
            </Typography>
            <Typography variant="h4">
              {rootNode?.name ?? t("common:placeholder")}
            </Typography>
            <Typography variant="caption" color="text.secondary">
              {t("cards.folders.layoutCaption")}
            </Typography>
          </CardContent>
        </Card>

        <Card>
          <CardContent>
            <Typography variant="overline" color="text.secondary">
              {t("cards.folders.title")}
            </Typography>
            <Typography variant="h4">
              {stats
                ? stats.nodeCount.toLocaleString()
                : t("common:placeholder")}
            </Typography>
            <Typography variant="caption" color="text.secondary">
              {t("cards.folders.caption")}
            </Typography>
          </CardContent>
        </Card>

        <Card>
          <CardContent>
            <Typography variant="overline" color="text.secondary">
              {t("cards.files.title")}
            </Typography>
            <Typography variant="h4">
              {stats
                ? stats.fileCount.toLocaleString()
                : t("common:placeholder")}
            </Typography>
            <Typography variant="caption" color="text.secondary">
              {t("cards.files.caption")}
            </Typography>
          </CardContent>
        </Card>

        <Card>
          <CardContent>
            <Typography variant="overline" color="text.secondary">
              {t("cards.data.title")}
            </Typography>
            <Typography variant="h4">
              {stats ? formatBytes(stats.sizeBytes) : t("common:placeholder")}
            </Typography>
            <Typography variant="caption" color="text.secondary">
              {t("cards.data.caption")}
            </Typography>
          </CardContent>
        </Card>
      </Box>
    </Box>
  );
};
