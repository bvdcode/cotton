import { afterEach, describe, expect, it, vi } from "vitest";
import {
  LIGHTBOX_MEDIA_SELECTOR,
  stopLightboxMediaPlayback,
} from "./mediaLightboxPlayback";

describe("mediaLightboxPlayback", () => {
  afterEach(() => {
    document.body.innerHTML = "";
  });

  it("pauses videos scoped to the media lightbox", () => {
    document.body.innerHTML =
      '<div class="lightbox-autohide"><video></video><video></video></div>' +
      '<div><video></video></div>';

    const lightboxVideos = Array.from(
      document.querySelectorAll<HTMLVideoElement>(LIGHTBOX_MEDIA_SELECTOR),
    );
    const outsideVideo = document.querySelector<HTMLVideoElement>(
      "body > div:not(.lightbox-autohide) video",
    );

    for (const video of [...lightboxVideos, outsideVideo]) {
      if (!video) continue;
      video.pause = vi.fn();
    }

    stopLightboxMediaPlayback();

    expect(lightboxVideos).toHaveLength(2);
    for (const video of lightboxVideos) {
      expect(video.pause).toHaveBeenCalledTimes(1);
    }
    expect(outsideVideo?.pause).not.toHaveBeenCalled();
  });
});
