/**
 * File type detection utilities for preview system
 */

import { isCodePreviewFileName } from "./codeFileTypes";
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

export interface FileTypeOptions {
  requiresVideoTranscoding?: boolean;
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
  "lrc",
  "srt",
];
const DOCUMENT_EXTENSIONS = ["doc", "docx", "rtf", "odt"];
const ARCHIVE_EXTENSIONS = ["zip", "rar", "7z", "tar", "gz"];
const PREVIEWABLE_FILE_TYPES = new Set<FileType>([
  "image",
  "model",
  "pdf",
  "video",
  "audio",
  "text",
]);
const EXTENSION_TYPE_MATCHERS: Array<[readonly string[], FileType]> = [
  [IMAGE_EXTENSIONS, "image"],
  [PDF_EXTENSIONS, "pdf"],
  [VIDEO_EXTENSIONS, "video"],
  [AUDIO_EXTENSIONS, "audio"],
  [TEXT_EXTENSIONS, "text"],
  [DOCUMENT_EXTENSIONS, "document"],
  [ARCHIVE_EXTENSIONS, "archive"],
];

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
  return TEXT_EXTENSIONS.includes(ext) || isCodePreviewFileName(fileName);
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

const toFileTypeInfo = (type: FileType): FileTypeInfo => ({
  type,
  supportsPreview: PREVIEWABLE_FILE_TYPES.has(type),
  supportsInlineView: PREVIEWABLE_FILE_TYPES.has(type),
});

const getFileTypeFromExtension = (
  fileName: string,
  ext: string,
  contentType?: string | null,
): FileType => {
  if (resolveModelFormat(fileName, contentType)) {
    return "model";
  }

  if (isCodePreviewFileName(fileName)) {
    return "text";
  }

  const match = EXTENSION_TYPE_MATCHERS.find(([extensions]) =>
    extensions.includes(ext),
  );
  return match?.[1] ?? "other";
};

const shouldSuppressUnplayableVideo = (
  ext: string,
  contentType?: string | null,
  requiresVideoTranscoding = false,
): boolean =>
  contentType?.toLowerCase().startsWith("video/") === true &&
  !VIDEO_EXTENSIONS.includes(ext) &&
  !requiresVideoTranscoding;

const shouldForceVideoPreview = (
  contentType?: string | null,
  requiresVideoTranscoding = false,
): boolean =>
  requiresVideoTranscoding &&
  contentType?.toLowerCase().startsWith("video/") === true;

export const getFileTypeInfo = (
  fileName: string,
  contentType?: string | null,
  options?: FileTypeOptions,
): FileTypeInfo => {
  const ext = getFileExtension(fileName);
  const requiresVideoTranscoding = options?.requiresVideoTranscoding === true;

  if (
    shouldSuppressUnplayableVideo(ext, contentType, requiresVideoTranscoding)
  ) {
    return toFileTypeInfo("other");
  }

  if (shouldForceVideoPreview(contentType, requiresVideoTranscoding)) {
    return toFileTypeInfo("video");
  }

  if (SVG_EXTENSIONS.includes(ext)) {
    return toFileTypeInfo("image");
  }

  const contentTypeMatch = getFileTypeFromContentType(contentType ?? undefined);
  if (contentTypeMatch) {
    return toFileTypeInfo(contentTypeMatch);
  }

  return toFileTypeInfo(getFileTypeFromExtension(fileName, ext, contentType));
};
