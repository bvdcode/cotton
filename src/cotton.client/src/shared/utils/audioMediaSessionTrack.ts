import type {
  AudioMediaSessionTrack,
  AudioPlaylistItem,
} from "../types/audio";

const stripFileExtension = (fileName: string): string => {
  const lastDot = fileName.lastIndexOf(".");
  if (lastDot <= 0) {
    return fileName;
  }
  return fileName.slice(0, lastDot);
};

const inferFolderMetadata = (
  folderPath: string | undefined,
): Pick<AudioMediaSessionTrack, "artist" | "album"> => {
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
): AudioMediaSessionTrack => {
  const title = stripFileExtension(item.name).trim() || item.name;
  const folderMetadata = inferFolderMetadata(item.folderPath);

  return {
    title,
    ...folderMetadata,
    artwork: item.previewUrl ? { src: item.previewUrl } : undefined,
  };
};
