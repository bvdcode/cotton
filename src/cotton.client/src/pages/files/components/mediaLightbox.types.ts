import type { Slide } from "yet-another-react-lightbox";

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

export type SlideWithTitle = Slide & {
  fileId: string;
  fileName: string;
  title?: string;
};
