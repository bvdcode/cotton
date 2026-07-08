import type { NodeFileManifestDto } from "../api/nodesApi";
import type { AudioPlaylistItem } from "../types/audio";

const mediaTitleKey = "media.title";
const mediaArtistKey = "media.artist";
const mediaAlbumKey = "media.album";
const mediaAlbumArtistKey = "media.albumArtist";
const mediaTrackKey = "media.track";
const mediaDiscKey = "media.disc";
const mediaDurationSecondsKey = "media.durationSeconds";

const readMetadataValue = (
  metadata: Record<string, string> | undefined,
  key: string,
): string | undefined => {
  const value = metadata?.[key]?.trim();
  return value ? value : undefined;
};

export const getAudioPlaylistMetadata = (
  file: Pick<NodeFileManifestDto, "metadata">,
): Pick<
  AudioPlaylistItem,
  "title" | "artist" | "album" | "albumArtist" | "track" | "disc" | "durationSeconds"
> => {
  const metadata = file.metadata;

  return {
    title: readMetadataValue(metadata, mediaTitleKey),
    artist: readMetadataValue(metadata, mediaArtistKey),
    album: readMetadataValue(metadata, mediaAlbumKey),
    albumArtist: readMetadataValue(metadata, mediaAlbumArtistKey),
    track: readMetadataValue(metadata, mediaTrackKey),
    disc: readMetadataValue(metadata, mediaDiscKey),
    durationSeconds: readMetadataValue(metadata, mediaDurationSecondsKey),
  };
};

export const getAudioDisplayTitle = (item: AudioPlaylistItem): string => {
  const title = item.title?.trim();
  return title ? title : item.name;
};

export const getAudioDisplaySubtitle = (
  item: AudioPlaylistItem,
): string | undefined => {
  const artist = item.artist?.trim();
  const album = item.album?.trim();

  if (artist && album) {
    return `${artist} - ${album}`;
  }

  return artist || album || undefined;
};
