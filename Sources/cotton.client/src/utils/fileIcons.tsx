import {
  Code,
  Image,
  Movie,
  Archive,
  Android,
  Audiotrack,
  Description,
  PictureAsPdf,
  InsertDriveFile,
} from "@mui/icons-material";
import type { ReactElement } from "react";

export function fileIcon(name: string, contentType?: string): ReactElement {
  const ct = contentType || "";
  const ext = name.split(".").pop()?.toLowerCase();

  // MIME-based
  if (ct === "application/pdf") return <PictureAsPdf color="error" />;
  if (ct.startsWith("image/")) return <Image color="info" />;
  if (ct.startsWith("video/")) return <Movie color="action" />;
  if (ct.startsWith("audio/")) return <Audiotrack color="secondary" />;
  if (ct === "application/zip" || ct === "application/x-zip-compressed")
    return <Archive />;
  if (ct === "application/json") return <Code />;
  if (ct === "text/plain") return <Description />;
  if (ct === "application/vnd.android.package-archive")
    return <Android color="success" />;

  // Extension-based fallbacks
  switch (ext) {
    case "jpg":
    case "jpeg":
    case "png":
    case "gif":
    case "bmp":
    case "webp":
      return <Image color="info" />;
    case "mp4":
    case "avi":
    case "mov":
    case "mkv":
      return <Movie color="action" />;
    case "mp3":
    case "wav":
    case "flac":
      return <Audiotrack color="secondary" />;
    case "zip":
    case "rar":
    case "7z":
      return <Archive />;
    case "pdf":
      return <PictureAsPdf color="error" />;
    case "apk":
      return <Android color="success" />;
    case "txt":
      return <Description />;
    case "json":
    case "js":
    case "ts":
    case "tsx":
    case "jsx":
      return <Code />;
    default:
      return <InsertDriveFile />;
  }
}
