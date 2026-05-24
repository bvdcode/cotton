export const LIGHTBOX_MEDIA_SELECTOR = ".lightbox-autohide video";

export const stopLightboxMediaPlayback = (): void => {
  if (typeof document === "undefined") return;

  document
    .querySelectorAll<HTMLVideoElement>(LIGHTBOX_MEDIA_SELECTOR)
    .forEach((video) => {
      video.pause();
      video.removeAttribute("src");
      video.querySelectorAll("source").forEach((source) => {
        source.removeAttribute("src");
      });
      video.load();
    });
};
