import { describe, expect, it } from "vitest";
import {
  getAudioDisplaySubtitle,
  getAudioDisplayTitle,
  getAudioPlaylistMetadata,
} from "./mediaMetadata";

describe("mediaMetadata", () => {
  it("maps extracted title, artist, and album into the audio player", () => {
    const metadata = getAudioPlaylistMetadata({
      metadata: {
        "media.title": "Pipeline title",
        "media.artist": "Pipeline artist",
        "media.album": "Pipeline album",
        "media.albumArtist": "Pipeline album artist",
        "media.track": "2/12",
        "media.disc": "1/2",
        "media.durationSeconds": "42.5",
      },
    });
    const item = {
      id: "track-1",
      name: "fallback.mp3",
      url: "/api/v1/files/track-1/content",
      ...metadata,
    };

    expect(metadata).toEqual({
      title: "Pipeline title",
      artist: "Pipeline artist",
      album: "Pipeline album",
      albumArtist: "Pipeline album artist",
      track: "2/12",
      disc: "1/2",
      durationSeconds: "42.5",
    });
    expect(getAudioDisplayTitle(item)).toBe("Pipeline title");
    expect(getAudioDisplaySubtitle(item)).toBe(
      "Pipeline artist - Pipeline album",
    );
  });

  it("uses the filename when valid media has no title tags", () => {
    const metadata = getAudioPlaylistMetadata({
      metadata: {
        "media.audioCodec": "mp3",
        "media.durationSeconds": "0.1",
      },
    });
    const item = {
      id: "track-2",
      name: "untagged.mp3",
      url: "/api/v1/files/track-2/content",
      ...metadata,
    };

    expect(getAudioDisplayTitle(item)).toBe("untagged.mp3");
    expect(getAudioDisplaySubtitle(item)).toBeUndefined();
  });
});
