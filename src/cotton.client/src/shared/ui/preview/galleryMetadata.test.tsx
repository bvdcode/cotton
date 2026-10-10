import { render, screen } from "@testing-library/react";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import i18n from "../../../i18n";
import {
  selectGalleryMetadataPosition,
  useUserPreferencesStore,
} from "../../store/userPreferencesStore";
import type { MediaItem, SlideWithTitle } from "../../types/mediaLightbox";
import {
  getGalleryMetadataEntries,
  getGalleryMetadataSections,
} from "./galleryMetadata";
import { MediaLightboxSlideHeader } from "./MediaLightboxSlideComponents";
import { buildSlidesFromItems } from "./mediaLightboxSlides";

const metadata = {
  "image.width": "6000",
  "image.height": "4000",
  "image.format": "JPEG",
  "image.Exif IFD0.0.tags.Model.272.description": "Camera Model",
  "image.Exif SubIFD.0.tags.F~1Number.33437.description": "f/2.8",
  "image.Exif IFD0.0.tags.Model.272": "raw model",
  metadataExtractorVersion: "1",
  "contentMetadata.extractionError": "internal error",
  en: "encrypted display metadata",
};

const slide: SlideWithTitle = {
  type: "image",
  src: "/photo.jpg",
  fileId: "photo",
  fileName: "photo • original.jpg",
  title: "1/2 • photo • original.jpg • 1 KB",
  sizeBytes: 1024,
  metadata,
};

beforeEach(async () => {
  vi.restoreAllMocks();
  useUserPreferencesStore.getState().reset();
  await i18n.changeLanguage("en");
});
afterEach(() => vi.unstubAllEnvs());

describe("gallery metadata", () => {
  it("converts ISO instants to the local time zone and preserves dates without an offset", () => {
    vi.stubEnv("TZ", "America/Los_Angeles");
    const format = (date: string) =>
      getGalleryMetadataEntries(
        { "media.date": date },
        i18n.getFixedT("en", "files"),
        "en",
      )[0].value.replace(/[\u00a0\u202f]/g, " ");
    expect(format("2026-10-08T01:30:00Z")).toBe("Oct 7, 2026, 6:30:00 PM");
    expect(format("2026-10-08T04:30:00+03:00")).toBe("Oct 7, 2026, 6:30:00 PM");
    expect(format("2026-10-08T01:30:00")).toBe("Oct 8, 2026, 1:30:00 AM");
    expect(format("2026-10-08")).toBe("Oct 8, 2026");
    expect(format("2026:10:08 01:30:00")).toBe("2026:10:08 01:30:00");
    expect(format("2026-13-08T01:30:00Z")).toBe("2026-13-08T01:30:00Z");
    const entries = getGalleryMetadataEntries(
      {
        "image.Exif SubIFD.0.tags.Date~1Time Original.36867.description":
          "2026-10-08T01:30:00Z",
      },
      i18n.getFixedT("ru", "files"),
      "ru",
    );
    expect(entries[0].value).toContain("7 окт. 2026");
    expect(entries[0].value).toContain("18:30:00");
  });
  it("defaults to the right edge and respects saved profile preferences", () => {
    expect(
      selectGalleryMetadataPosition(useUserPreferencesStore.getState()),
    ).toBe("right");
    useUserPreferencesStore
      .getState()
      .hydrateFromRemote({ galleryMetadataPosition: "right" });
    expect(
      selectGalleryMetadataPosition(useUserPreferencesStore.getState()),
    ).toBe("right");
    useUserPreferencesStore
      .getState()
      .hydrateFromRemote({ galleryMetadataPosition: "left" });
    expect(
      selectGalleryMetadataPosition(useUserPreferencesStore.getState()),
    ).toBe("left");
  });

  it("keeps file information in the header without a metadata panel", () => {
    render(<MediaLightboxSlideHeader slide={slide} />);
    expect(screen.getByText("photo • original.jpg")).toBeVisible();
    expect(screen.queryByText("6000 × 4000")).not.toBeInTheDocument();
    expect(screen.queryByText("internal error")).not.toBeInTheDocument();
    expect(screen.queryByText("raw model")).not.toBeInTheDocument();
    expect(
      screen.queryByText("encrypted display metadata"),
    ).not.toBeInTheDocument();
  });

  it("uses readable EXIF descriptions without internal or raw metadata", () => {
    const entries = getGalleryMetadataEntries(
      metadata,
      i18n.getFixedT("en", "files"),
      "en",
    );
    expect(entries).toContainEqual({
      key: "image.Exif SubIFD.0.tags.F~1Number.33437.description",
      label: "Exif SubIFD · F.Number",
      value: "f/2.8",
    });
    expect(
      entries.some(
        (entry) =>
          entry.value === "internal error" ||
          entry.value === "raw model" ||
          entry.value === "encrypted display metadata",
      ),
    ).toBe(false);
  });

  it("keeps PNG internals and maker notes out of the summary while retaining all details", () => {
    const sections = getGalleryMetadataSections(
      {
        ...metadata,
        "image.PNG-IHDR.0.tags.Color Type.4.description": "True Color",
        "image.PNG-IHDR.0.tags.Image Width.1.description": "539",
        "image.File Type.0.tags.Detected MIME Type.3.description": "image/png",
        "image.Exif SubIFD.0.tags.Exposure Time.33434.description": "1/125 sec",
        "image.Exif SubIFD.0.tags.ISO Speed Ratings.34855.description": "200",
        "image.Exif IFD1.0.tags.Model.272.description": "Thumbnail camera",
        "image.Some Maker Note.0.tags.Model.272.description":
          "Maker-specific value",
      },
      i18n.getFixedT("en", "files"),
      "en",
    );
    expect(sections.summary.map((entry) => entry.label)).toEqual([
      "Dimensions",
      "Format",
      "Camera",
      "Aperture",
      "Shutter speed",
      "ISO",
    ]);
    expect(sections.details.map((entry) => entry.value)).toEqual([
      "True Color",
      "539",
      "image/png",
      "Thumbnail camera",
      "Maker-specific value",
    ]);
    expect(
      sections.summary.some((entry) => entry.value === "internal error"),
    ).toBe(false);
  });

  it("formats video metadata with the active language", async () => {
    await i18n.changeLanguage("ru");
    const entries = getGalleryMetadataEntries(
      {
        "media.width": "1920",
        "media.height": "1080",
        "media.durationSeconds": "12.5",
        "media.videoCodec": "h264",
        "media.audioCodec": "aac",
      },
      i18n.getFixedT("ru", "files"),
      "ru",
    );
    expect(entries).toContainEqual({
      key: "duration",
      label: "Длительность",
      value: "12,5 с",
    });
    expect(entries).toContainEqual({
      key: "dimensions",
      label: "Разрешение",
      value: "1920 × 1080",
    });
    expect(entries).toContainEqual({
      key: "media.videoCodec",
      label: "Видеокодек",
      value: "h264",
    });
  });

  it.each([
    { kind: "image", signed: false, transcoding: false, active: true },
    { kind: "video", signed: false, transcoding: false, active: true },
    { kind: "video", signed: true, transcoding: false, active: true },
    { kind: "video", signed: true, transcoding: true, active: true },
    { kind: "video", signed: true, transcoding: true, active: false },
  ] as const)(
    "preserves metadata for $kind slides ($signed, $transcoding, $active)",
    (scenario) => {
      const item: MediaItem = {
        id: "media",
        kind: scenario.kind,
        name: "media",
        previewUrl: "/preview.jpg",
        mimeType: "video/mp4",
        metadata,
        requiresTranscoding: scenario.transcoding,
        sizeBytes: 1024,
      };
      const slides = buildSlidesFromItems(
        [item],
        {},
        scenario.signed ? { media: "/media.mp4" } : {},
        scenario.active ? "media" : null,
      );
      expect(slides[0].metadata).toBe(metadata);
      expect(slides[0].sizeBytes).toBe(1024);
    },
  );
});
