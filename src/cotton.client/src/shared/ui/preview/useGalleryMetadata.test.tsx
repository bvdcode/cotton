import React from "react";
import { act, fireEvent, render, screen } from "@testing-library/react";
import { beforeEach, describe, expect, it, vi } from "vitest";
import i18n from "../../../i18n";
import type { MediaItem } from "../../types/mediaLightbox";
import { useUserPreferencesStore } from "../../store/userPreferencesStore";
import { useGalleryMetadata } from "./useGalleryMetadata";

vi.mock("yet-another-react-lightbox", () => ({
  IconButton: React.forwardRef<
    HTMLButtonElement,
    React.ComponentProps<"button"> & { label: string }
  >(
    (
      {
        label,
        onClick,
        className,
        "aria-expanded": expanded,
        "aria-controls": controls,
      },
      ref,
    ) => (
      <button
        ref={ref}
        className={className}
        aria-label={label}
        aria-expanded={expanded}
        aria-controls={controls}
        onClick={onClick}
      />
    ),
  ),
}));

const photo: MediaItem = {
  id: "photo",
  kind: "image",
  name: "photo.jpg",
  previewUrl: "",
  mimeType: "image/jpeg",
  metadata: {
    "image.width": "6000",
    "image.height": "4000",
    "image.Exif IFD0.0.tags.Model.272.description": "Camera Model",
  },
};

const Gallery = ({
  item = photo,
  open = true,
  touch = false,
}: {
  item?: MediaItem;
  open?: boolean;
  touch?: boolean;
}) => {
  const metadata = useGalleryMetadata(item, open, touch);
  return (
    <div className="lightbox-autohide yarl__portal">
      {metadata.button}
      {metadata.controls}
    </div>
  );
};

beforeEach(async () => {
  useUserPreferencesStore.getState().reset();
  await i18n.changeLanguage("en");
});

const hoverEdge = (container: HTMLElement) => {
  const trigger = container.querySelector(".media-lightbox__metadata-trigger");
  if (!trigger) {
    throw new Error("Metadata edge is missing");
  }
  const event = new MouseEvent("pointerover", { bubbles: true });
  Object.defineProperty(event, "pointerType", { value: "mouse" });
  fireEvent(trigger, event);
};

describe("gallery metadata panel", () => {
  it.each(["left", "right"])(
    "opens only on hover at the selected %s edge, then closes on leave",
    (position) => {
      useUserPreferencesStore
        .getState()
        .hydrateFromRemote({ galleryMetadataPosition: position });
      const { container } = render(<Gallery />);
      expect(
        screen.queryByRole("region", { name: "Metadata" }),
      ).not.toBeInTheDocument();
      expect(
        screen.queryByRole("button", { name: "Metadata" }),
      ).not.toBeInTheDocument();
      expect(
        container.querySelector(`.media-lightbox__metadata-edge--${position}`),
      ).toBeInTheDocument();
      hoverEdge(container);
      expect(screen.getByRole("region", { name: "Metadata" })).toBeVisible();
      expect(screen.getByText("Camera Model")).toBeVisible();
      const edge = container.querySelector(".media-lightbox__metadata-edge");
      if (!edge) {
        throw new Error("Metadata edge is missing");
      }
      fireEvent.pointerLeave(edge);
      expect(
        screen.queryByRole("region", { name: "Metadata" }),
      ).not.toBeInTheDocument();
    },
  );

  it("opens a modal by a button on touch screens without edge panels", () => {
    const { container } = render(<Gallery touch />);
    const button = screen.getByRole("button", { name: "Metadata" });
    expect(button).toHaveAttribute("aria-expanded", "false");
    fireEvent.click(button);
    expect(button).toHaveAttribute("aria-expanded", "true");
    expect(
      container.querySelector(".media-lightbox__metadata-edge"),
    ).not.toBeInTheDocument();
    expect(screen.getByRole("dialog", { name: "Metadata" })).toBeVisible();
    fireEvent.keyDown(screen.getByRole("dialog", { name: "Metadata" }), {
      key: "Escape",
    });
    expect(button).toHaveAttribute("aria-expanded", "false");
    expect(button).toHaveFocus();
  });

  it("removes hover controls and the touch button in hidden mode", () => {
    useUserPreferencesStore
      .getState()
      .hydrateFromRemote({ galleryMetadataPosition: "hidden" });
    const { container } = render(<Gallery touch />);
    expect(
      container.querySelector(".media-lightbox__metadata-edge"),
    ).not.toBeInTheDocument();
    expect(
      screen.queryByRole("button", { name: "Metadata" }),
    ).not.toBeInTheDocument();
  });

  it("updates metadata when changing slides and starts closed when the gallery reopens", () => {
    const { rerender } = render(<Gallery touch />);
    fireEvent.click(screen.getByRole("button", { name: "Metadata" }));
    rerender(
      <Gallery
        touch
        item={{ ...photo, name: "next.jpg", metadata: undefined }}
      />,
    );
    expect(screen.getByText("next.jpg")).toBeVisible();
    expect(screen.queryByText("Camera Model")).not.toBeInTheDocument();
    rerender(<Gallery touch open={false} />);
    rerender(<Gallery touch />);
    expect(
      screen.queryByRole("dialog", { name: "Metadata" }),
    ).not.toBeInTheDocument();
  });

  it("moves to the selected side without keeping the previous panel open", () => {
    const { container } = render(<Gallery />);
    hoverEdge(container);
    act(() =>
      useUserPreferencesStore
        .getState()
        .hydrateFromRemote({ galleryMetadataPosition: "left" }),
    );
    expect(
      container.querySelector(".media-lightbox__metadata-edge--left"),
    ).toBeInTheDocument();
    expect(
      screen.queryByRole("region", { name: "Metadata" }),
    ).not.toBeInTheDocument();
  });
});
