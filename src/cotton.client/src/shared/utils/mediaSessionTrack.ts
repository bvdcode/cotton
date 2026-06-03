import type { MediaItem } from "@shared/types/mediaLightbox";
import type { AudioPlaylistItem } from "../types/audio";
import type { MediaSessionTrackInfo } from "../types/mediaSession";

const stripFileExtension = (fileName: string): string => {
  const lastDot = fileName.lastIndexOf(".");
  if (lastDot <= 0) {
    return fileName;
  }
  return fileName.slice(0, lastDot);
};

const buildFileTitle = (fileName: string): string => {
  return stripFileExtension(fileName).trim() || fileName;
};

const inferAudioFolderMetadata = (
  folderPath: string | undefined,
): Pick<MediaSessionTrackInfo, "artist" | "album"> => {
  if (!folderPath) {
    return {};
  }

  const segments = folderPath
    .split("/")
    .map((segment) => segment.trim())
    .filter((segment) => segment.length > 0);

  if (segments.length < 2) {
    return {};
  }

  return {
    artist: segments[0],
    album: segments[segments.length - 1],
  };
};

export const buildAudioMediaSessionTrack = (
  item: AudioPlaylistItem,
): MediaSessionTrackInfo => {
  const folderMetadata = inferAudioFolderMetadata(item.folderPath);

  return {
    title: buildFileTitle(item.name),
    ...folderMetadata,
    artwork: item.previewUrl ? { src: item.previewUrl } : undefined,
  };
};

export const buildVideoMediaSessionTrack = (
  item: MediaItem,
): MediaSessionTrackInfo => {
  return {
    title: buildFileTitle(item.name),
    artwork: item.previewUrl ? { src: item.previewUrl } : undefined,
  };
};
