export interface AudioPlaylistItem {
  id: string;
  name: string;
  nodeId?: string;
  folderPath?: string;
  previewUrl?: string;
}

export interface AudioMediaSessionArtwork {
  src: string;
  type?: string;
}

export interface AudioMediaSessionTrack {
  title: string;
  artist?: string;
  album?: string;
  artwork?: AudioMediaSessionArtwork;
}
