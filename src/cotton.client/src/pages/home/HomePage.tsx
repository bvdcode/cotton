import { Box, Card, CardContent, Typography, Alert } from "@mui/material";
import { useQueryClient } from "@tanstack/react-query";
import { useCallback } from "react";
import { useTranslation } from "react-i18next";
import Loader from "../../shared/ui/Loader";
import {
  invalidateLayoutOverview,
  useLayoutStatsQuery,
  useRecentFilesQuery,
  useRootNodeQuery,
} from "../../shared/api/queries/layouts";
import {
  HUB_METHODS,
  useFileTreeRealtimeInvalidation,
  type HubMethodOrLower,
} from "../../shared/signalr";
import { formatBytes } from "../../shared/utils/formatBytes";
import { useAuth } from "../../features/auth";
import { RecentFilesCard } from "./components/RecentFilesCard";

const HOME_OVERVIEW_METHODS = new Set<string>(
  [
    HUB_METHODS.FileCreated,
    HUB_METHODS.FileUpdated,
    HUB_METHODS.FileDeleted,
    HUB_METHODS.FileMoved,
    HUB_METHODS.FileRenamed,
    HUB_METHODS.FileRestored,
    HUB_METHODS.NodeCreated,
    HUB_METHODS.NodeDeleted,
    HUB_METHODS.NodeMoved,
    HUB_METHODS.NodeRestored,
  ].map((method) => method.toLowerCase()),
);

const shouldInvalidateHomeOverview = (method: HubMethodOrLower): boolean =>
  HOME_OVERVIEW_METHODS.has(method.toLowerCase());

export const HomePage: React.FC = () => {
  const { t } = useTranslation(["home", "common"]);
  const { isAuthenticated } = useAuth();
  const queryClient = useQueryClient();
  const rootQuery = useRootNodeQuery();
  const rootNode = rootQuery.data ?? null;
  const layoutId = rootNode?.layoutId;
  const statsQuery = useLayoutStatsQuery(layoutId);
  const recentQuery = useRecentFilesQuery(layoutId);

  const stats = statsQuery.data;
  const recentFiles = recentQuery.data ?? [];
  const loadingRoot = rootQuery.isPending;
  const loadingStats = statsQuery.isPending && !!layoutId;
  const loadingRecent = recentQuery.isPending && !!layoutId;
  const handleRealtimeInvalidate = useCallback((): void => {
    if (layoutId) {
      void invalidateLayoutOverview(queryClient, layoutId);
    }
  }, [layoutId, queryClient]);
  useFileTreeRealtimeInvalidation({
    enabled: isAuthenticated && Boolean(layoutId),
    onInvalidate: handleRealtimeInvalidate,
    shouldInvalidate: shouldInvalidateHomeOverview,
  });
  const error = rootQuery.error
    ? "Failed to resolve root layout"
    : statsQuery.error
      ? "Failed to load layout stats"
      : null;
  const isLoading = loadingRoot || loadingStats;

  if (isLoading && !rootNode && !stats) {
    return <Loader title={t("loading.title")} caption={t("loading.caption")} />;
  }

  return (
    <Box
      pt={{
        xs: 1,
        md: 3,
      }}
      width="100%"
    >
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

        <RecentFilesCard files={recentFiles} loading={loadingRecent} />
      </Box>
    </Box>
  );
};
