import { act, render, screen } from "@testing-library/react";
import type { ReactElement } from "react";
import { describe, expect, it, vi } from "vitest";
import { MediaLightbox } from "./MediaLightbox";
import type { MediaItem } from "@shared/types/mediaLightbox";

type CapturedLightboxProps = {
  close: () => void;
  open: boolean;
  slides: ReadonlyArray<object>;
  video?: {
    autoPlay?: boolean;
  };
};

const capturedLightboxProps: CapturedLightboxProps[] = [];

vi.mock("react-i18next", () => ({
  useTranslation: () => ({
    t: (key: string) => key,
  }),
}));

vi.mock("yet-another-react-lightbox", () => ({
  default: (props: CapturedLightboxProps): ReactElement => {
    capturedLightboxProps.push(props);
    return <button onClick={props.close}>close</button>;
  },
}));

vi.mock("yet-another-react-lightbox/plugins/video", () => ({ default: {} }));
vi.mock("yet-another-react-lightbox/plugins/download", () => ({ default: {} }));
vi.mock("yet-another-react-lightbox/plugins/zoom", () => ({ default: {} }));
vi.mock("yet-another-react-lightbox/plugins/slideshow", () => ({ default: {} }));
vi.mock("yet-another-react-lightbox/plugins/thumbnails", () => ({ default: {} }));
vi.mock("yet-another-react-lightbox/plugins/share", () => ({ default: {} }));
vi.mock("../../hooks/useActivityDetection", () => ({
  useActivityDetection: () => true,
}));
vi.mock("../../store/userPreferencesStore", () => ({
  selectGalleryPreferPreview: false,
  useUserPreferencesStore: () => false,
}));
vi.mock("./useMediaLightboxUrls", () => ({
  useMediaLightboxUrls: () => ({
    slides: [
      {
        type: "video",
        sources: [{ src: "/video.mp4", type: "video/mp4" }],
      },
    ],
    ensureSlideHasOriginal: vi.fn(),
    handleSlideImageError: vi.fn(),
    resolveSlideDownloadUrl: vi.fn(),
  }),
}));

const mediaItems: MediaItem[] = [
  {
    id: "video-id",
    kind: "video",
    name: "video.mp4",
    previewUrl: "",
    mimeType: "video/mp4",
  },
];

describe("MediaLightbox", () => {
  it("removes playable slides before closing so videos cannot resume in the background", () => {
    capturedLightboxProps.length = 0;
    const onClose = vi.fn();

    render(
      <MediaLightbox
        items={mediaItems}
        open
        initialIndex={0}
        onClose={onClose}
        getSignedMediaUrl={vi.fn()}
      />,
    );

    expect(capturedLightboxProps.at(-1)?.slides).toHaveLength(1);
    expect(capturedLightboxProps.at(-1)?.video?.autoPlay).toBe(true);

    act(() => {
      screen.getByRole("button", { name: "close" }).click();
    });

    expect(onClose).toHaveBeenCalledTimes(1);
    expect(capturedLightboxProps.at(-1)?.slides).toHaveLength(0);
    expect(capturedLightboxProps.at(-1)?.video?.autoPlay).toBe(false);
  });
});
