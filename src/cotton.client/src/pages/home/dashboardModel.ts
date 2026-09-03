import { z } from "zod";
import type { NodeFileManifestDto } from "../../shared/api/nodesApi";
import { getFileTypeInfo, type FileType } from "../../shared/utils/fileTypes";

export const DASHBOARD_WIDGET_IDS = [
  "overview",
  "pinnedFolders",
  "quickAccess",
  "recentFiles",
  "recentImages",
  "recentVideos",
  "recentDocuments",
  "recentAudio",
  "recentOther",
] as const;

export type DashboardWidgetId = (typeof DASHBOARD_WIDGET_IDS)[number];

export type RecentFilesWidgetId = Extract<
  DashboardWidgetId,
  | "recentFiles"
  | "recentImages"
  | "recentVideos"
  | "recentDocuments"
  | "recentAudio"
  | "recentOther"
>;

export interface RecentFilesFilter {
  contentTypes?: readonly string[];
  excludedContentTypes?: readonly string[];
}

export const RECENT_FILES_FILTERS: Record<
  RecentFilesWidgetId,
  RecentFilesFilter
> = {
  recentFiles: {},
  recentImages: { contentTypes: ["image/*"] },
  recentVideos: { contentTypes: ["video/*"] },
  recentDocuments: {
    contentTypes: [
      "application/pdf",
      "text/*",
      "application/msword",
      "application/rtf",
      "application/vnd.ms-excel",
      "application/vnd.ms-powerpoint",
      "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
      "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
      "application/vnd.openxmlformats-officedocument.presentationml.presentation",
      "application/vnd.oasis.opendocument.text",
      "application/vnd.oasis.opendocument.spreadsheet",
      "application/vnd.oasis.opendocument.presentation",
    ],
  },
  recentAudio: { contentTypes: ["audio/*"] },
  recentOther: {
    excludedContentTypes: [
      "image/*",
      "video/*",
      "audio/*",
      "application/pdf",
      "text/*",
      "application/msword",
      "application/rtf",
      "application/vnd.ms-excel",
      "application/vnd.ms-powerpoint",
      "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
      "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
      "application/vnd.openxmlformats-officedocument.presentationml.presentation",
      "application/vnd.oasis.opendocument.text",
      "application/vnd.oasis.opendocument.spreadsheet",
      "application/vnd.oasis.opendocument.presentation",
    ],
  },
};

export interface DashboardLayout {
  order: DashboardWidgetId[];
  hidden: DashboardWidgetId[];
}

const widgetIdSchema = z.enum(DASHBOARD_WIDGET_IDS);
const dashboardLayoutSchema = z.object({
  order: z.array(widgetIdSchema),
  hidden: z.array(widgetIdSchema),
});

export const DEFAULT_DASHBOARD_LAYOUT: DashboardLayout = {
  order: [...DASHBOARD_WIDGET_IDS],
  hidden: [],
};

const unique = (ids: readonly DashboardWidgetId[]): DashboardWidgetId[] => [
  ...new Set(ids),
];

export const parseDashboardLayout = (value: string | undefined): DashboardLayout => {
  if (!value) {
    return DEFAULT_DASHBOARD_LAYOUT;
  }

  try {
    const parsed = dashboardLayoutSchema.safeParse(JSON.parse(value));
    if (!parsed.success) {
      return DEFAULT_DASHBOARD_LAYOUT;
    }

    const hidden = unique(parsed.data.hidden);
    const hiddenSet = new Set<DashboardWidgetId>(hidden);
    const savedOrder = unique(parsed.data.order).filter(
      (widgetId) => !hiddenSet.has(widgetId),
    );
    const represented = new Set<DashboardWidgetId>([...savedOrder, ...hidden]);
    const newWidgets = DASHBOARD_WIDGET_IDS.filter(
      (widgetId) => !represented.has(widgetId),
    );

    return {
      order: [...savedOrder, ...newWidgets],
      hidden,
    };
  } catch {
    return DEFAULT_DASHBOARD_LAYOUT;
  }
};

export const serializeDashboardLayout = (layout: DashboardLayout): string =>
  JSON.stringify(layout);

export const moveDashboardWidget = (
  layout: DashboardLayout,
  sourceId: DashboardWidgetId,
  targetId: DashboardWidgetId,
): DashboardLayout => {
  const sourceIndex = layout.order.indexOf(sourceId);
  const targetIndex = layout.order.indexOf(targetId);
  if (sourceIndex < 0 || targetIndex < 0 || sourceIndex === targetIndex) {
    return layout;
  }

  const order = [...layout.order];
  const [source] = order.splice(sourceIndex, 1);
  order.splice(targetIndex, 0, source);
  return { ...layout, order };
};

export const hideDashboardWidget = (
  layout: DashboardLayout,
  widgetId: DashboardWidgetId,
): DashboardLayout => {
  if (!layout.order.includes(widgetId)) {
    return layout;
  }

  return {
    order: layout.order.filter((candidate) => candidate !== widgetId),
    hidden: [...layout.hidden, widgetId],
  };
};

export const restoreDashboardWidget = (
  layout: DashboardLayout,
  widgetId: DashboardWidgetId,
): DashboardLayout => {
  if (!layout.hidden.includes(widgetId)) {
    return layout;
  }

  return {
    order: [...layout.order, widgetId],
    hidden: layout.hidden.filter((candidate) => candidate !== widgetId),
  };
};

const DOCUMENT_TYPES = new Set<FileType>(["pdf", "text", "document"]);
const OTHER_TYPES = new Set<FileType>(["model", "archive", "other"]);

export const filterRecentFiles = (
  files: readonly NodeFileManifestDto[],
  widgetId: RecentFilesWidgetId,
): NodeFileManifestDto[] => {
  if (widgetId === "recentFiles") {
    return [...files];
  }

  return files.filter((file) => {
    const type = getFileTypeInfo(file.name, file.contentType, {
      requiresVideoTranscoding: file.requiresVideoTranscoding,
    }).type;

    switch (widgetId) {
      case "recentImages":
        return type === "image";
      case "recentVideos":
        return type === "video";
      case "recentDocuments":
        return DOCUMENT_TYPES.has(type);
      case "recentAudio":
        return type === "audio";
      case "recentOther":
        return OTHER_TYPES.has(type);
      default:
        throw new Error(`Unsupported recent files widget: ${widgetId}`);
    }
  });
};
