export interface MediaSessionArtworkInfo {
  src: string;
  type?: string;
}

export interface MediaSessionTrackInfo {
  title: string;
  artist?: string;
  album?: string;
  artwork?: MediaSessionArtworkInfo;
}
