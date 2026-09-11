import type { TFunction } from "i18next";
import type { DashboardWidgetId, RecentFilesWidgetId } from "./dashboardModel";

export const RECENT_WIDGET_IDS = new Set<DashboardWidgetId>([
  "recentFiles",
  "recentImages",
  "recentVideos",
  "recentDocuments",
  "recentAudio",
  "recentOther",
]);

export const isRecentFilesWidget = (
  widgetId: DashboardWidgetId,
): widgetId is RecentFilesWidgetId => RECENT_WIDGET_IDS.has(widgetId);

export const getDashboardWidgetTitle = (
  t: TFunction,
  widgetId: DashboardWidgetId,
): string => {
  switch (widgetId) {
    case "overview":
      return t("dashboard.widgets.overview");
    case "pinnedFolders":
      return t("dashboard.widgets.pinnedFolders");
    case "quickAccess":
      return t("dashboard.widgets.quickAccess");
    case "recentFiles":
      return t("dashboard.widgets.recentFiles");
    case "recentImages":
      return t("dashboard.widgets.recentImages");
    case "recentVideos":
      return t("dashboard.widgets.recentVideos");
    case "recentDocuments":
      return t("dashboard.widgets.recentDocuments");
    case "recentAudio":
      return t("dashboard.widgets.recentAudio");
    case "recentOther":
      return t("dashboard.widgets.recentOther");
    default:
      throw new Error(`Unsupported dashboard widget: ${widgetId}`);
  }
};
