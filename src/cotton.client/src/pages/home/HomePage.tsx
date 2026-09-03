import { DashboardCustomize, Done } from "@mui/icons-material";
import { Alert, Box, Button, Stack, Typography } from "@mui/material";
import { useQueryClient } from "@tanstack/react-query";
import { useCallback, useState } from "react";
import { useTranslation } from "react-i18next";
import Loader from "../../shared/ui/Loader";
import {
  invalidateLayoutOverview,
  useLayoutStatsQuery,
  useRootNodeQuery,
} from "../../shared/api/queries/layouts";
import { useFileTreeRealtimeInvalidation } from "../../shared/signalr";
import { useAuth } from "../../features/auth";
import { usePinnedFolders } from "../../shared/dashboard/usePinnedFolders";
import { useDashboardLayout } from "./useDashboardLayout";
import { DashboardWidgetLibrary } from "./components/DashboardWidgetLibrary";
import { HomeDashboard } from "./HomeDashboard";

export const HomePage: React.FC = () => {
  const { t } = useTranslation(["home", "common"]);
  const { isAuthenticated } = useAuth();
  const queryClient = useQueryClient();
  const [customizing, setCustomizing] = useState(false);
  const dashboard = useDashboardLayout();
  const pinnedFolders = usePinnedFolders();
  const rootQuery = useRootNodeQuery();
  const rootNode = rootQuery.data ?? null;
  const layoutId = rootNode?.layoutId;
  const statsQuery = useLayoutStatsQuery(layoutId);

  const stats = statsQuery.data;
  const loadingRoot = rootQuery.isPending;
  const loadingStats = statsQuery.isPending && !!layoutId;
  const handleRealtimeInvalidate = useCallback((): void => {
    if (layoutId) {
      void invalidateLayoutOverview(queryClient, layoutId);
    }
  }, [layoutId, queryClient]);
  useFileTreeRealtimeInvalidation({
    enabled: isAuthenticated && Boolean(layoutId),
    onInvalidate: handleRealtimeInvalidate,
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

      <Stack
        direction={{ xs: "column", sm: "row" }}
        alignItems={{ xs: "stretch", sm: "flex-end" }}
        justifyContent="space-between"
        gap={1}
        mb={2}
      >
        <div>
          <Typography variant="overline" color="text.secondary">
            {t("title")}
          </Typography>
          <Typography variant="h4">{t("dashboard.title")}</Typography>
        </div>
        <Button
          variant={customizing ? "contained" : "outlined"}
          startIcon={customizing ? <Done /> : <DashboardCustomize />}
          onClick={() => setCustomizing((current) => !current)}
        >
          {customizing
            ? t("dashboard.actions.done")
            : t("dashboard.actions.customize")}
        </Button>
      </Stack>

      <HomeDashboard
        customizing={customizing}
        dashboard={dashboard}
        layoutId={layoutId}
        pinnedFolders={pinnedFolders}
        stats={stats}
        translate={t}
      />

      {customizing && (
        <DashboardWidgetLibrary
          hiddenWidgetIds={dashboard.layout.hidden}
          onRestore={dashboard.restore}
        />
      )}
    </Box>
  );
};
