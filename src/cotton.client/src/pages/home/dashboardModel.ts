import { z } from "zod";

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

export const DASHBOARD_WIDGET_SIZES = [1, 2, 3] as const;

export type DashboardWidgetSize = (typeof DASHBOARD_WIDGET_SIZES)[number];
export type DashboardWidgetSizes = Record<
  DashboardWidgetId,
  DashboardWidgetSize
>;

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

const IMAGE_CONTENT_TYPES = ["image/*"] as const;
const VIDEO_CONTENT_TYPES = ["video/*"] as const;
const DOCUMENT_CONTENT_TYPES = [
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
] as const;
const AUDIO_CONTENT_TYPES = ["audio/*"] as const;
const CATEGORIZED_CONTENT_TYPES = [
  ...IMAGE_CONTENT_TYPES,
  ...VIDEO_CONTENT_TYPES,
  ...DOCUMENT_CONTENT_TYPES,
  ...AUDIO_CONTENT_TYPES,
] as const;

export const RECENT_FILES_FILTERS: Record<
  RecentFilesWidgetId,
  RecentFilesFilter
> = {
  recentFiles: {},
  recentImages: { contentTypes: IMAGE_CONTENT_TYPES },
  recentVideos: { contentTypes: VIDEO_CONTENT_TYPES },
  recentDocuments: { contentTypes: DOCUMENT_CONTENT_TYPES },
  recentAudio: { contentTypes: AUDIO_CONTENT_TYPES },
  recentOther: {
    excludedContentTypes: CATEGORIZED_CONTENT_TYPES,
  },
};

export interface DashboardLayout {
  order: DashboardWidgetId[];
  hidden: DashboardWidgetId[];
  sizes: DashboardWidgetSizes;
}

const widgetIdSchema = z.enum(DASHBOARD_WIDGET_IDS);
const widgetSizeSchema = z.union([z.literal(1), z.literal(2), z.literal(3)]);
const dashboardLayoutSchema = z.object({
  order: z.array(widgetIdSchema),
  hidden: z.array(widgetIdSchema),
  sizes: z.record(z.string(), z.unknown()).optional(),
});

export const DEFAULT_DASHBOARD_WIDGET_SIZES: DashboardWidgetSizes = {
  overview: 1,
  pinnedFolders: 2,
  quickAccess: 1,
  recentFiles: 2,
  recentImages: 2,
  recentVideos: 1,
  recentDocuments: 1,
  recentAudio: 1,
  recentOther: 1,
};

export const DEFAULT_DASHBOARD_LAYOUT: DashboardLayout = {
  order: [...DASHBOARD_WIDGET_IDS],
  hidden: [],
  sizes: { ...DEFAULT_DASHBOARD_WIDGET_SIZES },
};

const unique = (ids: readonly DashboardWidgetId[]): DashboardWidgetId[] => [
  ...new Set(ids),
];

export const parseDashboardLayout = (
  value: string | undefined,
): DashboardLayout => {
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
    const sizes = Object.fromEntries(
      DASHBOARD_WIDGET_IDS.map((widgetId) => {
        const parsedSize = widgetSizeSchema.safeParse(
          parsed.data.sizes?.[widgetId],
        );
        return [
          widgetId,
          parsedSize.success
            ? parsedSize.data
            : DEFAULT_DASHBOARD_WIDGET_SIZES[widgetId],
        ];
      }),
    ) as DashboardWidgetSizes;

    return {
      order: [...savedOrder, ...newWidgets],
      hidden,
      sizes,
    };
  } catch {
    return DEFAULT_DASHBOARD_LAYOUT;
  }
};

export const serializeDashboardLayout = (layout: DashboardLayout): string =>
  JSON.stringify(layout);

export const resizeDashboardWidget = (
  layout: DashboardLayout,
  widgetId: DashboardWidgetId,
  size: DashboardWidgetSize,
): DashboardLayout => {
  if (layout.sizes[widgetId] === size) {
    return layout;
  }

  return {
    ...layout,
    sizes: {
      ...layout.sizes,
      [widgetId]: size,
    },
  };
};

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
    ...layout,
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
    ...layout,
    order: [...layout.order, widgetId],
    hidden: layout.hidden.filter((candidate) => candidate !== widgetId),
  };
};
