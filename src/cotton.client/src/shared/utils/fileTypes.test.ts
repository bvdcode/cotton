import { describe, expect, it } from "vitest";
import {
  getFileExtension,
  getFileTypeInfo,
  isAudioFile,
  isImageFile,
  isPdfFile,
  isTextFile,
  isVideoFile,
} from "./fileTypes";

describe("getFileExtension", () => {
  it("returns the lowercase final extension segment", () => {
    expect(getFileExtension("Report.PDF")).toBe("pdf");
    expect(getFileExtension("archive.tar.GZ")).toBe("gz");
  });

  it("returns the original lowercase name when there is no dot", () => {
    expect(getFileExtension("README")).toBe("readme");
  });
});

describe("file type predicates", () => {
  it("recognizes image files", () => {
    expect(isImageFile("photo.jpg")).toBe(true);
    expect(isImageFile("photo.PNG")).toBe(true);
    expect(isImageFile("photo.heic")).toBe(true);
    expect(isImageFile("document.pdf")).toBe(false);
  });

  it("recognizes pdf files", () => {
    expect(isPdfFile("document.pdf")).toBe(true);
    expect(isPdfFile("document.PDF")).toBe(true);
    expect(isPdfFile("image.png")).toBe(false);
  });

  it("recognizes only supported inline video extensions", () => {
    expect(isVideoFile("clip.mp4")).toBe(true);
    expect(isVideoFile("clip.webm")).toBe(true);
    expect(isVideoFile("clip.avi")).toBe(false);
  });

  it("recognizes supported audio extensions", () => {
    expect(isAudioFile("song.mp3")).toBe(true);
    expect(isAudioFile("song.flac")).toBe(true);
    expect(isAudioFile("song.aac")).toBe(false);
  });

  it("recognizes text-like extensions", () => {
    expect(isTextFile("notes.md")).toBe(true);
    expect(isTextFile("data.json")).toBe(true);
    expect(isTextFile("config.yml")).toBe(true);
    expect(isTextFile("document.docx")).toBe(false);
  });
});

describe("getFileTypeInfo", () => {
  it("treats SVG as an inline image even with XML content type", () => {
    expect(getFileTypeInfo("logo.svg", "text/xml")).toEqual({
      type: "image",
      supportsPreview: true,
      supportsInlineView: true,
    });
  });

  it("downgrades unsupported video containers reported by content type", () => {
    expect(getFileTypeInfo("clip.avi", "video/x-msvideo")).toEqual({
      type: "other",
      supportsPreview: false,
      supportsInlineView: false,
    });
  });

  it("classifies supported extensions", () => {
    expect(getFileTypeInfo("clip.mp4").type).toBe("video");
    expect(getFileTypeInfo("song.mp3").type).toBe("audio");
    expect(getFileTypeInfo("document.pdf").type).toBe("pdf");
    expect(getFileTypeInfo("notes.md").type).toBe("text");
  });

  it("classifies documents and archives as non-previewable", () => {
    expect(getFileTypeInfo("report.docx")).toMatchObject({
      type: "document",
      supportsPreview: false,
    });
    expect(getFileTypeInfo("logs.zip")).toMatchObject({
      type: "archive",
      supportsPreview: false,
    });
  });

  it("falls back to other for unknown extensions", () => {
    expect(getFileTypeInfo("data.xyz")).toEqual({
      type: "other",
      supportsPreview: false,
      supportsInlineView: false,
    });
  });
});
