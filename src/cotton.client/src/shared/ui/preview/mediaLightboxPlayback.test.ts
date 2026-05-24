import { afterEach, describe, expect, it, vi } from "vitest";
import {
  LIGHTBOX_MEDIA_SELECTOR,
  stopLightboxMediaPlayback,
} from "./mediaLightboxPlayback";

describe("mediaLightboxPlayback", () => {
  afterEach(() => {
    document.body.innerHTML = "";
  });

  it("aborts pending videos scoped to the media lightbox", () => {
    document.body.innerHTML =
      '<div class="lightbox-autohide">' +
      '<video src="/preview/a"><source src="/preview/a.mp4" /></video>' +
      '<video src="/preview/b"><source src="/preview/b.mp4" /></video>' +
      "</div>" +
      '<div><video src="/outside"><source src="/outside.mp4" /></video></div>';

    const lightboxVideos = Array.from(
      document.querySelectorAll<HTMLVideoElement>(LIGHTBOX_MEDIA_SELECTOR),
    );
    const outsideVideo = document.querySelector<HTMLVideoElement>(
      "body > div:not(.lightbox-autohide) video",
    );

    for (const video of [...lightboxVideos, outsideVideo]) {
      if (!video) continue;
      video.pause = vi.fn();
      video.load = vi.fn();
    }

    stopLightboxMediaPlayback();

    expect(lightboxVideos).toHaveLength(2);
    for (const video of lightboxVideos) {
      expect(video.pause).toHaveBeenCalledTimes(1);
      expect(video.load).toHaveBeenCalledTimes(1);
      expect(video.hasAttribute("src")).toBe(false);
      expect(video.querySelector("source")?.hasAttribute("src")).toBe(false);
    }
    expect(outsideVideo?.pause).not.toHaveBeenCalled();
    expect(outsideVideo?.load).not.toHaveBeenCalled();
    expect(outsideVideo?.getAttribute("src")).toBe("/outside");
    expect(outsideVideo?.querySelector("source")?.getAttribute("src")).toBe(
      "/outside.mp4",
    );
  });
});
