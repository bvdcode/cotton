import { describe, expect, it } from "vitest";
import {
  DASHBOARD_WIDGET_IDS,
  DEFAULT_DASHBOARD_LAYOUT,
  DEFAULT_DASHBOARD_WIDGET_SIZES,
  RECENT_FILES_FILTERS,
  hideDashboardWidget,
  moveDashboardWidget,
  parseDashboardLayout,
  resizeDashboardWidget,
  restoreDashboardWidget,
  serializeDashboardLayout,
} from "./dashboardModel";

describe("dashboard layout", () => {
  it("shows every supported widget by default", () => {
    expect(DEFAULT_DASHBOARD_LAYOUT).toEqual({
      order: DASHBOARD_WIDGET_IDS,
      hidden: [],
      sizes: DEFAULT_DASHBOARD_WIDGET_SIZES,
    });
  });

  it("restores a valid persisted layout and appends newly introduced widgets", () => {
    const stored = JSON.stringify({
      order: ["recentVideos", "overview"],
      hidden: ["recentFiles"],
    });

    const layout = parseDashboardLayout(stored);

    expect(layout.order.slice(0, 2)).toEqual(["recentVideos", "overview"]);
    expect(layout.hidden).toEqual(["recentFiles"]);
    expect(layout.sizes).toEqual(DEFAULT_DASHBOARD_WIDGET_SIZES);
    expect([...layout.order, ...layout.hidden].sort()).toEqual(
      [...DASHBOARD_WIDGET_IDS].sort(),
    );
  });

  it("restores valid widget sizes and defaults missing or invalid sizes", () => {
    const stored = JSON.stringify({
      order: ["overview", "recentFiles"],
      hidden: [],
      sizes: {
        overview: 3,
        recentFiles: 4,
      },
    });

    const layout = parseDashboardLayout(stored);

    expect(layout.sizes.overview).toBe(3);
    expect(layout.sizes.recentFiles).toBe(
      DEFAULT_DASHBOARD_WIDGET_SIZES.recentFiles,
    );
    expect(layout.sizes.quickAccess).toBe(
      DEFAULT_DASHBOARD_WIDGET_SIZES.quickAccess,
    );
  });

  it("falls back to the complete default for malformed preferences", () => {
    expect(parseDashboardLayout("not-json")).toEqual(DEFAULT_DASHBOARD_LAYOUT);
    expect(
      parseDashboardLayout(JSON.stringify({ order: ["missing"] })),
    ).toEqual(DEFAULT_DASHBOARD_LAYOUT);
  });

  it("moves, hides, restores, and serializes widgets without losing entries", () => {
    const moved = moveDashboardWidget(
      DEFAULT_DASHBOARD_LAYOUT,
      "recentFiles",
      "overview",
    );
    expect(moved.order[0]).toBe("recentFiles");

    const hidden = hideDashboardWidget(moved, "recentVideos");
    expect(hidden.hidden).toContain("recentVideos");
    expect(hidden.order).not.toContain("recentVideos");

    const restored = restoreDashboardWidget(hidden, "recentVideos");
    expect(restored.hidden).not.toContain("recentVideos");
    expect(restored.order.at(-1)).toBe("recentVideos");
    const resized = resizeDashboardWidget(restored, "recentVideos", 3);
    expect(resized.sizes.recentVideos).toBe(3);
    expect(resized.sizes.overview).toBe(
      DEFAULT_DASHBOARD_WIDGET_SIZES.overview,
    );
    expect(parseDashboardLayout(serializeDashboardLayout(resized))).toEqual(
      resized,
    );
  });
});

describe("recent file categories", () => {
  it("uses server filters for every category card", () => {
    expect(RECENT_FILES_FILTERS.recentImages).toEqual({
      contentTypes: ["image/*"],
    });
    expect(RECENT_FILES_FILTERS.recentVideos).toEqual({
      contentTypes: ["video/*"],
    });
    expect(RECENT_FILES_FILTERS.recentDocuments.contentTypes).toContain(
      "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
    );
    expect(RECENT_FILES_FILTERS.recentOther.excludedContentTypes).toEqual(
      expect.arrayContaining(["image/*", "video/*", "audio/*"]),
    );
    const categorizedContentTypes = [
      ...(RECENT_FILES_FILTERS.recentImages.contentTypes ?? []),
      ...(RECENT_FILES_FILTERS.recentVideos.contentTypes ?? []),
      ...(RECENT_FILES_FILTERS.recentDocuments.contentTypes ?? []),
      ...(RECENT_FILES_FILTERS.recentAudio.contentTypes ?? []),
    ];
    expect(RECENT_FILES_FILTERS.recentOther.excludedContentTypes).toEqual(
      categorizedContentTypes,
    );
  });
});
