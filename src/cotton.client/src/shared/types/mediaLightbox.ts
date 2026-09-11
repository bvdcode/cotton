import type { GenericSlide, Slide } from "yet-another-react-lightbox";

type MediaKind = "image" | "video";

export interface MediaItem {
  id: string;
  kind: MediaKind;
  name: string;
  previewUrl: string;
  width?: number;
  height?: number;
  mimeType: string;
  sizeBytes?: number;
  requiresTranscoding?: boolean;
}

export interface MediaLightboxSourceFile {
  id: string;
  name: string;
  sizeBytes?: number;
  previewHashEncryptedHex?: string | null;
  largeFilePreviewPresignedToken?: string | null;
  contentType?: string | null;
  requiresVideoTranscoding?: boolean;
}

export interface MediaVideoSource {
  src: string;
  type: string;
}

export interface MediaLightboxProps {
  items: MediaItem[];
  open: boolean;
  initialIndex: number;
  onClose: () => void;
  getSignedMediaUrl: (id: string) => Promise<string>;
  smoothTransitions?: boolean;
  getDownloadUrl?: (id: string) => Promise<string>;
  onDelete?: (item: MediaItem) => void | Promise<void>;
}

export const HLS_VIDEO_SLIDE_TYPE = "video-hls" as const;

export interface SlideHlsVideo extends GenericSlide {
  type: "video-hls";
  src: string;
  poster?: string;
  width?: number;
  height?: number;
}

declare module "yet-another-react-lightbox" {
  interface SlideTypes {
    "video-hls": SlideHlsVideo;
  }

  interface Labels {
    Delete?: string;
  }
}

export type SlideWithTitle = Slide & {
  fileId: string;
  fileName: string;
  title?: string;
};

export const isSlideWithTitle = (slide: Slide): slide is SlideWithTitle =>
  "fileId" in slide &&
  typeof slide.fileId === "string" &&
  "fileName" in slide &&
  typeof slide.fileName === "string";
