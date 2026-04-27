/**
 * File type detection utilities for preview system
 */

import { resolveModelFormat } from "./modelFormats";

export type FileType =
  | "image"
  | "model"
  | "pdf"
  | "video"
  | "audio"
  | "text"
  | "document"
  | "archive"
  | "other";

export interface FileTypeInfo {
  type: FileType;
  supportsPreview: boolean;
  supportsInlineView: boolean;
}

const IMAGE_EXTENSIONS = [
  "jpg",
  "jpeg",
  "png",
  "gif",
  "webp",
  "bmp",
  "svg",
  "svgz",
  "heic",
];
const SVG_EXTENSIONS = ["svg", "svgz"];
const PDF_EXTENSIONS = ["pdf"];
// Only include formats that are generally playable inline in modern browsers.
// Keep this list conservative to avoid opening the media lightbox for files the browser can't play.
const VIDEO_EXTENSIONS = ["mp4", "m4v", "webm", "mov"];
const AUDIO_EXTENSIONS = ["mp3", "wav", "ogg", "flac", "m4a"];
const TEXT_EXTENSIONS = [
  "txt",
  "md",
  "markdown",
  "json",
  "xml",
  "csv",
  "log",
  "yaml",
  "yml",
  "ini",
  "conf",
  "config",
  "inf",
];
const DOCUMENT_EXTENSIONS = ["doc", "docx", "rtf", "odt"];
const ARCHIVE_EXTENSIONS = ["zip", "rar", "7z", "tar", "gz"];

const SUPPORTED_INLINE_VIDEO_MIME_TYPES = new Set<string>([
  "video/mp4",
  "video/webm",
  "video/quicktime",
  "video/x-quicktime",
  "video/mov",
  "video/x-mov",
]);

export const getFileExtension = (fileName: string): string => {
  return fileName.toLowerCase().split(".").pop() || "";
};

export const isImageFile = (fileName: string): boolean => {
  const ext = getFileExtension(fileName);
  return IMAGE_EXTENSIONS.includes(ext);
};

export const isPdfFile = (fileName: string): boolean => {
  const ext = getFileExtension(fileName);
  return PDF_EXTENSIONS.includes(ext);
};

export const isVideoFile = (fileName: string): boolean => {
  const ext = getFileExtension(fileName);
  return VIDEO_EXTENSIONS.includes(ext);
};

export const isAudioFile = (fileName: string): boolean => {
  const ext = getFileExtension(fileName);
  return AUDIO_EXTENSIONS.includes(ext);
};

export const isTextFile = (fileName: string): boolean => {
  const ext = getFileExtension(fileName);
  return TEXT_EXTENSIONS.includes(ext);
};

const getFileTypeFromContentType = (contentType?: string): FileType | null => {
  if (!contentType) return null;
  const normalized = contentType.toLowerCase().split(";")[0]?.trim() ?? "";

  if (normalized.startsWith("image/")) return "image";
  if (resolveModelFormat("", normalized)) return "model";
  if (SUPPORTED_INLINE_VIDEO_MIME_TYPES.has(normalized)) return "video";
  if (normalized.startsWith("audio/")) return "audio";
  if (normalized.startsWith("text/")) return "text";
  if (normalized === "application/pdf") return "pdf";

  if (
    normalized.includes("zip") ||
    normalized.includes("rar") ||
    normalized.includes("7z") ||
    normalized.includes("tar") ||
    normalized.includes("gzip")
  ) {
    return "archive";
  }

  if (
    normalized.includes("msword") ||
    normalized.includes("officedocument") ||
    normalized.includes("opendocument") ||
    normalized.includes("rtf")
  ) {
    return "document";
  }

  return null;
};

export const getFileTypeInfo = (
  fileName: string,
  contentType?: string | null,
): FileTypeInfo => {
  const ext = getFileExtension(fileName);

  // Browser can't reliably play many containers inline; keep video preview conservative.
  // If the extension is not in our allow-list, do not treat it as previewable video.
  if (
    contentType?.toLowerCase().startsWith("video/") === true &&
    !VIDEO_EXTENSIONS.includes(ext)
  ) {
    return { type: "other", supportsPreview: false, supportsInlineView: false };
  }

  // SVGs can arrive with XML/text MIME aliases, but they should still open in the image gallery.
  if (SVG_EXTENSIONS.includes(ext)) {
    return { type: "image", supportsPreview: true, supportsInlineView: true };
  }

  const contentTypeMatch = getFileTypeFromContentType(contentType ?? undefined);

  if (contentTypeMatch) {
    switch (contentTypeMatch) {
      case "image":
        return { type: "image", supportsPreview: true, supportsInlineView: true };
      case "model":
        return { type: "model", supportsPreview: true, supportsInlineView: true };
      case "pdf":
        return { type: "pdf", supportsPreview: true, supportsInlineView: true };
      case "video":
        return { type: "video", supportsPreview: true, supportsInlineView: true };
      case "audio":
        return { type: "audio", supportsPreview: true, supportsInlineView: true };
      case "text":
        return { type: "text", supportsPreview: true, supportsInlineView: true };
      case "document":
        return { type: "document", supportsPreview: false, supportsInlineView: false };
      case "archive":
        return { type: "archive", supportsPreview: false, supportsInlineView: false };
      default:
        return { type: "other", supportsPreview: false, supportsInlineView: false };
    }
  }

  if (IMAGE_EXTENSIONS.includes(ext)) {
    return { type: "image", supportsPreview: true, supportsInlineView: true };
  }
  if (resolveModelFormat(fileName, contentType)) {
    return { type: "model", supportsPreview: true, supportsInlineView: true };
  }
  if (PDF_EXTENSIONS.includes(ext)) {
    return { type: "pdf", supportsPreview: true, supportsInlineView: true };
  }
  if (VIDEO_EXTENSIONS.includes(ext)) {
    return { type: "video", supportsPreview: true, supportsInlineView: true };
  }
  if (AUDIO_EXTENSIONS.includes(ext)) {
    return { type: "audio", supportsPreview: true, supportsInlineView: true };
  }
  if (TEXT_EXTENSIONS.includes(ext)) {
    return { type: "text", supportsPreview: true, supportsInlineView: true };
  }
  if (DOCUMENT_EXTENSIONS.includes(ext)) {
    return {
      type: "document",
      supportsPreview: false,
      supportsInlineView: false,
    };
  }
  if (ARCHIVE_EXTENSIONS.includes(ext)) {
    return {
      type: "archive",
      supportsPreview: false,
      supportsInlineView: false,
    };
  }

  return { type: "other", supportsPreview: false, supportsInlineView: false };
};
