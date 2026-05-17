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

export interface MediaLightboxProps {
  items: MediaItem[];
  open: boolean;
  initialIndex: number;
  onClose: () => void;
  getSignedMediaUrl: (id: string) => Promise<string>;
  smoothTransitions?: boolean;
  getDownloadUrl?: (id: string) => Promise<string>;
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
}

export type SlideWithTitle = Slide & {
  fileId: string;
  fileName: string;
  title?: string;
};
