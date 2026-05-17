import { describe, expect, it } from "vitest";
import { HLS_VIDEO_SLIDE_TYPE } from "./mediaLightbox.types";
import type { MediaItem } from "./mediaLightbox.types";
import { buildSlidesFromItems } from "./mediaLightboxSlides";

describe("buildSlidesFromItems", () => {
  it("renders the active transcodable video as a custom hls slide", () => {
    const item = createVideoItem({ requiresTranscoding: true });
    const slides = buildSlidesFromItems(
      [item],
      {},
      { video: "/api/v1/files/video/hls/master.m3u8?token=t" },
      "video",
    );

    expect(slides[0]).toMatchObject({
      fileId: "video",
      fileName: "clip.avi",
      type: HLS_VIDEO_SLIDE_TYPE,
      src: "/api/v1/files/video/hls/master.m3u8?token=t",
      poster: "/preview.webp",
    });
  });

  it("hides hls source from inactive transcodable slides", () => {
    const item = createVideoItem({ requiresTranscoding: true });
    const slides = buildSlidesFromItems(
      [item],
      {},
      { video: "/api/v1/files/video/hls/master.m3u8?token=t" },
      null,
    );

    expect(slides[0]).toMatchObject({
      type: "image",
      src: "/preview.webp",
    });
  });

  it("keeps native browser videos on the lightbox video plugin path", () => {
    const item = createVideoItem({
      mimeType: "video/mp4",
      name: "clip.mp4",
      requiresTranscoding: false,
    });
    const slides = buildSlidesFromItems(
      [item],
      {},
      { video: "/api/v1/files/video/download?token=t" },
      "video",
    );

    expect(slides[0]).toMatchObject({
      type: "video",
      sources: [{ src: "/api/v1/files/video/download?token=t", type: "video/mp4" }],
    });
  });
});

const createVideoItem = (
  overrides: Partial<MediaItem> = {},
): MediaItem => ({
  id: "video",
  kind: "video",
  name: "clip.avi",
  previewUrl: "/preview.webp",
  mimeType: "video/x-msvideo",
  sizeBytes: 1024,
  ...overrides,
});
