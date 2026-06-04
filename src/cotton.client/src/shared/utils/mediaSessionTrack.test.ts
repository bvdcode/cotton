import { describe, expect, it } from "vitest";
import {
  buildAudioMediaSessionTrack,
  buildVideoMediaSessionTrack,
} from "./mediaSessionTrack";

describe("media session track builders", () => {
  it("uses the audio file name without the final extension as the title", () => {
    expect(
      buildAudioMediaSessionTrack({
        id: "track-1",
        name: "01 - Intro.demo.mp3",
      }).title,
    ).toBe("01 - Intro.demo");
  });

  it("keeps the original name when stripping would produce an empty title", () => {
    expect(
      buildAudioMediaSessionTrack({
        id: "track-1",
        name: ".mp3",
      }).title,
    ).toBe(".mp3");
  });

  it("uses audio previewUrl as media session artwork", () => {
    expect(
      buildAudioMediaSessionTrack({
        id: "track-1",
        name: "Track.flac",
        previewUrl: "/api/v1/preview/cover.webp",
      }).artwork,
    ).toEqual({ src: "/api/v1/preview/cover.webp" });
  });

  it("infers audio artist and album only from multi-level folder paths", () => {
    expect(
      buildAudioMediaSessionTrack({
        id: "track-1",
        name: "Track.flac",
        folderPath: "Artist / Album",
      }),
    ).toMatchObject({ artist: "Artist", album: "Album" });

    expect(
      buildAudioMediaSessionTrack({
        id: "track-2",
        name: "Track.flac",
        folderPath: "Maybe Artist Or Album",
      }),
    ).not.toHaveProperty("artist");
  });

  it("uses video names and posters without audio-only metadata guesses", () => {
    expect(
      buildVideoMediaSessionTrack({
        id: "video-1",
        kind: "video",
        name: "Clip.mov",
        previewUrl: "/api/v1/preview/poster.webp",
        mimeType: "video/quicktime",
      }),
    ).toEqual({
      title: "Clip",
      artwork: { src: "/api/v1/preview/poster.webp" },
    });
  });
});
