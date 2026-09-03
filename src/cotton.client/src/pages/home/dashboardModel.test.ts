import { describe, expect, it } from "vitest";
import type { NodeFileManifestDto } from "../../shared/api/nodesApi";
import {
  DASHBOARD_WIDGET_IDS,
  DEFAULT_DASHBOARD_LAYOUT,
  RECENT_FILES_FILTERS,
  filterRecentFiles,
  hideDashboardWidget,
  moveDashboardWidget,
  parseDashboardLayout,
  restoreDashboardWidget,
  serializeDashboardLayout,
} from "./dashboardModel";

const makeFile = (
  id: string,
  name: string,
  contentType: string,
): NodeFileManifestDto => ({
  id,
  createdAt: "2026-09-02T00:00:00Z",
  updatedAt: "2026-09-02T00:00:00Z",
  nodeId: "node-1",
  ownerId: "user-1",
  name,
  contentType,
  sizeBytes: 1,
  metadata: {},
});

describe("dashboard layout", () => {
  it("shows every supported widget by default", () => {
    expect(DEFAULT_DASHBOARD_LAYOUT).toEqual({
      order: DASHBOARD_WIDGET_IDS,
      hidden: [],
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
    expect([...layout.order, ...layout.hidden].sort()).toEqual(
      [...DASHBOARD_WIDGET_IDS].sort(),
    );
  });

  it("falls back to the complete default for malformed preferences", () => {
    expect(parseDashboardLayout("not-json")).toEqual(
      DEFAULT_DASHBOARD_LAYOUT,
    );
    expect(parseDashboardLayout(JSON.stringify({ order: ["missing"] }))).toEqual(
      DEFAULT_DASHBOARD_LAYOUT,
    );
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
    expect(parseDashboardLayout(serializeDashboardLayout(restored))).toEqual(
      restored,
    );
  });
});

describe("recent file categories", () => {
  const files = [
    makeFile("image", "photo.heic", "image/heic"),
    makeFile("video", "clip.mp4", "video/mp4"),
    makeFile("pdf", "contract.pdf", "application/pdf"),
    makeFile("text", "notes.md", "text/markdown"),
    makeFile("document", "letter.docx", "application/vnd.openxmlformats-officedocument.wordprocessingml.document"),
    makeFile("audio", "song.flac", "audio/flac"),
    makeFile("archive", "backup.zip", "application/zip"),
    makeFile("other", "disk.iso", "application/octet-stream"),
  ];

  it("keeps the all-files widget unfiltered", () => {
    expect(filterRecentFiles(files, "recentFiles")).toEqual(files);
  });

  it("separates photos, videos, documents, audio, and remaining files", () => {
    expect(filterRecentFiles(files, "recentImages").map((file) => file.id)).toEqual([
      "image",
    ]);
    expect(filterRecentFiles(files, "recentVideos").map((file) => file.id)).toEqual([
      "video",
    ]);
    expect(filterRecentFiles(files, "recentDocuments").map((file) => file.id)).toEqual([
      "pdf",
      "text",
      "document",
    ]);
    expect(filterRecentFiles(files, "recentAudio").map((file) => file.id)).toEqual([
      "audio",
    ]);
    expect(filterRecentFiles(files, "recentOther").map((file) => file.id)).toEqual([
      "archive",
      "other",
    ]);
  });

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
  });
});
