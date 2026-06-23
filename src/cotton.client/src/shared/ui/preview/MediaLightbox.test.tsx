import { act, fireEvent, render, screen } from "@testing-library/react";
import type { ReactElement, ReactNode } from "react";
import { describe, expect, it, vi } from "vitest";
import { MediaLightbox } from "./MediaLightbox";
import type { MediaItem } from "@shared/types/mediaLightbox";

type CapturedLightboxProps = {
  close: () => void;
  index?: number;
  open: boolean;
  slides: ReadonlyArray<object>;
  on?: {
    view?: (event: { index: number }) => void;
  };
  toolbar?: {
    buttons?: ReadonlyArray<ReactNode>;
  };
  video?: {
    autoPlay?: boolean;
  };
};

type MockIconButtonProps = {
  disabled?: boolean;
  label: string;
  onClick?: () => void;
  renderIcon?: () => ReactNode;
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
    return (
      <div>
        <button onClick={props.close}>close</button>
        {props.toolbar?.buttons?.map((button, index) =>
          typeof button === "string" ? null : <span key={index}>{button}</span>,
        )}
      </div>
    );
  },
  IconButton: ({
    disabled,
    label,
    onClick,
    renderIcon,
  }: MockIconButtonProps) => (
    <button aria-label={label} disabled={disabled} onClick={onClick}>
      {renderIcon?.()}
    </button>
  ),
}));

vi.mock("yet-another-react-lightbox/plugins/video", () => ({ default: {} }));
vi.mock("yet-another-react-lightbox/plugins/download", () => ({ default: {} }));
vi.mock("yet-another-react-lightbox/plugins/zoom", () => ({ default: {} }));
vi.mock("yet-another-react-lightbox/plugins/slideshow", () => ({
  default: {},
}));
vi.mock("yet-another-react-lightbox/plugins/thumbnails", () => ({
  default: {},
}));
vi.mock("yet-another-react-lightbox/plugins/share", () => ({ default: {} }));
vi.mock("../../hooks/useActivityDetection", () => ({
  useActivityDetection: () => true,
}));
vi.mock("../../store/userPreferencesStore", () => ({
  selectGalleryPreferPreview: false,
  useUserPreferencesStore: () => false,
}));
vi.mock("./useMediaLightboxUrls", () => ({
  useMediaLightboxUrls: ({ items }: { items: MediaItem[] }) => ({
    slides: items.map((item) =>
      item.kind === "video"
        ? {
            type: "video",
            sources: [{ src: `/${item.name}`, type: item.mimeType }],
          }
        : { type: "image", src: `/${item.name}` },
    ),
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

const makeImageItem = (id: string, name: string): MediaItem => ({
  id,
  kind: "image",
  name,
  previewUrl: "",
  mimeType: "image/jpeg",
});

const galleryItems: MediaItem[] = [
  makeImageItem("image-1", "first.jpg"),
  makeImageItem("image-2", "second.jpg"),
  makeImageItem("image-3", "third.jpg"),
];

describe("MediaLightbox", () => {
  it("removes playable slides before closing so videos cannot resume in the background", () => {
    capturedLightboxProps.length = 0;
    const onClose = vi.fn();

    const getSignedMediaUrl = vi.fn();
    const { rerender } = render(
      <MediaLightbox
        items={mediaItems}
        open
        initialIndex={0}
        onClose={onClose}
        getSignedMediaUrl={getSignedMediaUrl}
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

    act(() => {
      rerender(
        <MediaLightbox
          items={mediaItems}
          open={false}
          initialIndex={0}
          onClose={onClose}
          getSignedMediaUrl={getSignedMediaUrl}
        />,
      );
    });

    act(() => {
      rerender(
        <MediaLightbox
          items={mediaItems}
          open
          initialIndex={0}
          onClose={onClose}
          getSignedMediaUrl={getSignedMediaUrl}
        />,
      );
    });

    expect(capturedLightboxProps.at(-1)?.slides).toHaveLength(1);
    expect(capturedLightboxProps.at(-1)?.video?.autoPlay).toBe(true);
  });

  it("requests deleting the current item from the toolbar", () => {
    capturedLightboxProps.length = 0;
    const onDelete = vi.fn();

    render(
      <MediaLightbox
        items={mediaItems}
        open
        initialIndex={0}
        onClose={vi.fn()}
        getSignedMediaUrl={vi.fn()}
        onDelete={onDelete}
      />,
    );

    fireEvent.click(screen.getByRole("button", { name: "Delete" }));

    expect(onDelete).toHaveBeenCalledTimes(1);
    expect(onDelete).toHaveBeenCalledWith(
      expect.objectContaining({ id: "video-id", name: "video.mp4" }),
    );
  });

  it("keeps the next media item selected after the current item is removed", () => {
    capturedLightboxProps.length = 0;
    const { rerender } = render(
      <MediaLightbox
        items={galleryItems}
        open
        initialIndex={0}
        onClose={vi.fn()}
        getSignedMediaUrl={vi.fn()}
      />,
    );

    act(() => {
      capturedLightboxProps.at(-1)?.on?.view?.({ index: 1 });
    });

    rerender(
      <MediaLightbox
        items={[galleryItems[0], galleryItems[2]]}
        open
        initialIndex={0}
        onClose={vi.fn()}
        getSignedMediaUrl={vi.fn()}
      />,
    );

    expect(capturedLightboxProps.at(-1)?.index).toBe(1);
  });

  it("keeps the previous media item selected after the last item is removed", () => {
    capturedLightboxProps.length = 0;
    const { rerender } = render(
      <MediaLightbox
        items={galleryItems}
        open
        initialIndex={0}
        onClose={vi.fn()}
        getSignedMediaUrl={vi.fn()}
      />,
    );

    act(() => {
      capturedLightboxProps.at(-1)?.on?.view?.({ index: 2 });
    });

    rerender(
      <MediaLightbox
        items={[galleryItems[0], galleryItems[1]]}
        open
        initialIndex={0}
        onClose={vi.fn()}
        getSignedMediaUrl={vi.fn()}
      />,
    );

    expect(capturedLightboxProps.at(-1)?.index).toBe(1);
  });

  it("requests deleting the current item with the Delete key", () => {
    capturedLightboxProps.length = 0;
    const onDelete = vi.fn();

    render(
      <MediaLightbox
        items={mediaItems}
        open
        initialIndex={0}
        onClose={vi.fn()}
        getSignedMediaUrl={vi.fn()}
        onDelete={onDelete}
      />,
    );

    fireEvent.keyDown(document, { key: "Delete" });

    expect(onDelete).toHaveBeenCalledTimes(1);
    expect(onDelete).toHaveBeenCalledWith(
      expect.objectContaining({ id: "video-id", name: "video.mp4" }),
    );
  });
});
