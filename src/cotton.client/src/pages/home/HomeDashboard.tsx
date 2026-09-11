import { Box } from "@mui/material";
import type { TFunction } from "i18next";
import type { LayoutStatsDto } from "../../shared/api/layoutsApi";
import type { DashboardWidgetId } from "./dashboardModel";
import {
  getDashboardWidgetTitle,
  isRecentFilesWidget,
} from "./dashboardWidgetMetadata";
import type { useDashboardLayout } from "./useDashboardLayout";
import type { usePinnedFolders } from "../../shared/dashboard/usePinnedFolders";
import { DashboardOverviewWidget } from "./components/DashboardOverviewWidget";
import { DashboardPinnedFoldersWidget } from "./components/DashboardPinnedFoldersWidget";
import { DashboardQuickAccessWidget } from "./components/DashboardQuickAccessWidget";
import { DashboardRecentFilesWidget } from "./components/DashboardRecentFilesWidget";
import { DashboardWidgetFrame } from "./components/DashboardWidgetFrame";

interface HomeDashboardProps {
  customizing: boolean;
  dashboard: ReturnType<typeof useDashboardLayout>;
  layoutId: string | undefined;
  pinnedFolders: ReturnType<typeof usePinnedFolders>;
  stats: LayoutStatsDto | undefined;
  translate: TFunction;
}

export const HomeDashboard = ({
  customizing,
  dashboard,
  layoutId,
  pinnedFolders,
  stats,
  translate,
}: HomeDashboardProps) => {
  const renderWidget = (widgetId: DashboardWidgetId) => {
    if (widgetId === "overview") {
      return <DashboardOverviewWidget stats={stats} />;
    }
    if (widgetId === "pinnedFolders") {
      return (
        <DashboardPinnedFoldersWidget
          folderIds={pinnedFolders.folderIds}
          folders={pinnedFolders.folders}
          isError={pinnedFolders.foldersError}
          isPending={pinnedFolders.foldersPending}
          onRetry={() => void pinnedFolders.refetchFolders()}
          onUnpin={(folderId) => pinnedFolders.setPinned(folderId, false)}
        />
      );
    }
    if (widgetId === "quickAccess") {
      return <DashboardQuickAccessWidget />;
    }
    if (isRecentFilesWidget(widgetId)) {
      return (
        <DashboardRecentFilesWidget
          enabled
          layoutId={layoutId}
          size={dashboard.layout.sizes[widgetId]}
          widgetId={widgetId}
        />
      );
    }

    throw new Error(`Unsupported dashboard widget: ${widgetId}`);
  };

  return (
    <Box
      display="grid"
      gap={2}
      gridTemplateColumns={{ xs: "1fr", md: "repeat(12, minmax(0, 1fr))" }}
    >
      {dashboard.layout.order.map((widgetId, index) => (
        <DashboardWidgetFrame
          key={widgetId}
          widgetId={widgetId}
          title={getDashboardWidgetTitle(translate, widgetId)}
          size={dashboard.layout.sizes[widgetId]}
          customizing={customizing}
          first={index === 0}
          last={index === dashboard.layout.order.length - 1}
          onDragStart={dashboard.startDrag}
          onDragEnd={dashboard.endDrag}
          onDrop={dashboard.drop}
          onHide={dashboard.hide}
          onMove={dashboard.move}
          onResize={dashboard.resize}
        >
          {renderWidget(widgetId)}
        </DashboardWidgetFrame>
      ))}
    </Box>
  );
};
