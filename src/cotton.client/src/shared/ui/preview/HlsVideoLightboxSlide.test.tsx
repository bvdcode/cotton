import { render } from "@testing-library/react";
import { describe, expect, it, vi } from "vitest";
import { HlsVideoLightboxSlide } from "./MediaLightboxSlideComponents";
import {
  HLS_VIDEO_SLIDE_TYPE,
  type SlideHlsVideo,
  type SlideWithTitle,
} from "@shared/types/mediaLightbox";

describe("HlsVideoLightboxSlide", () => {
  it("keeps the active video registered across lightbox renders", () => {
    const slide: SlideHlsVideo & SlideWithTitle = {
      type: HLS_VIDEO_SLIDE_TYPE,
      src: "/video/master.m3u8",
      fileId: "video",
      fileName: "video.mkv",
    };
    const setActiveVideoElementForFile = vi.fn();
    const props = {
      currentItemId: slide.fileId,
      errorText: "Unable to play",
      noticeText: "Loading",
      offset: 0,
      setActiveVideoElementForFile,
      slide,
    };
    const { container, rerender, unmount } = render(
      <HlsVideoLightboxSlide {...props} />,
    );
    const element = container.querySelector("video");
    expect(element).not.toBeNull();
    expect(setActiveVideoElementForFile).toHaveBeenCalledExactlyOnceWith(
      slide.fileId,
      element,
    );

    rerender(<HlsVideoLightboxSlide {...props} noticeText="Preparing video" />);
    expect(setActiveVideoElementForFile).toHaveBeenCalledTimes(1);

    unmount();
    expect(setActiveVideoElementForFile).toHaveBeenCalledTimes(2);
    expect(setActiveVideoElementForFile).toHaveBeenLastCalledWith(
      slide.fileId,
      null,
    );
  });
});
