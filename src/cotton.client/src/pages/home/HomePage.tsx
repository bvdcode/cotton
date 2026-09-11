import { DashboardCustomize, Done } from "@mui/icons-material";
import { Alert, Box, Button } from "@mui/material";
import { useQueryClient } from "@tanstack/react-query";
import { useCallback, useState } from "react";
import { useTranslation } from "react-i18next";
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

      <HomeDashboard
        customizing={customizing}
        dashboard={dashboard}
        layoutId={layoutId}
        pinnedFolders={pinnedFolders}
        stats={stats}
        translate={t}
      />

      <Box display="flex" justifyContent="center" mt={2}>
        <Button
          variant={customizing ? "contained" : "outlined"}
          startIcon={customizing ? <Done /> : <DashboardCustomize />}
          onClick={() => setCustomizing((current) => !current)}
        >
          {customizing
            ? t("dashboard.actions.done")
            : t("dashboard.actions.customize")}
        </Button>
      </Box>

      {customizing && (
        <DashboardWidgetLibrary
          hiddenWidgetIds={dashboard.layout.hidden}
          onRestore={dashboard.restore}
        />
      )}
    </Box>
  );
};
