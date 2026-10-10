import React from "react";
import {
  act,
  fireEvent,
  render,
  screen,
  waitFor,
} from "@testing-library/react";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import i18n from "../../../i18n";
import type { MediaItem } from "../../types/mediaLightbox";
import { userPreferencesApi } from "../../api/userPreferencesApi";
import { useUserPreferencesStore } from "../../store/userPreferencesStore";
import { useGalleryMetadata } from "./useGalleryMetadata";

vi.mock("yet-another-react-lightbox", () => ({
  IconButton: React.forwardRef<
    HTMLButtonElement,
    React.ComponentProps<"button"> & { label: string }
  >(
    (
      {
        onClick,
        onKeyDown,
        className,
        "aria-label": label,
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
        onKeyDown={onKeyDown}
      />
    ),
  ),
}));

const photo: MediaItem = {
  id: "photo",
  kind: "image",
  name: "photo.png",
  previewUrl: "",
  mimeType: "image/png",
  metadata: {
    "image.width": "539",
    "image.height": "922",
    "image.format": "PNG",
    "image.Exif IFD0.0.tags.Model.272.description": "Camera Model",
    "image.PNG-IHDR.0.tags.Color Type.4.description": "True Color",
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
  vi.restoreAllMocks();
  useUserPreferencesStore.getState().reset();
  useUserPreferencesStore.getState().hydrateFromRemote({});
  vi.spyOn(userPreferencesApi, "update").mockImplementation(async (patch) => ({
    ...useUserPreferencesStore.getState().preferences,
    ...patch,
  }));
  await i18n.changeLanguage("en");
});

afterEach(async () => {
  await waitFor(() =>
    expect(useUserPreferencesStore.getState().syncing).toBe(false),
  );
});

describe("gallery information panel", () => {
  it("allows guest viewers to open the panel and details without authenticated requests", () => {
    useUserPreferencesStore.getState().reset();
    render(<Gallery />);
    fireEvent.click(screen.getByRole("button", { name: "Information" }));
    fireEvent.click(screen.getByRole("button", { name: "More details" }));
    expect(screen.getByText("True Color")).toBeVisible();
    expect(userPreferencesApi.update).not.toHaveBeenCalled();
  });

  it("keeps the information readable when saving preferences fails", async () => {
    vi.mocked(userPreferencesApi.update).mockRejectedValue(
      new Error("Preferences unavailable"),
    );
    render(<Gallery />);
    fireEvent.click(screen.getByRole("button", { name: "Information" }));
    fireEvent.click(screen.getByRole("button", { name: "More details" }));
    await waitFor(() =>
      expect(useUserPreferencesStore.getState().syncing).toBe(false),
    );
    expect(screen.getByRole("region", { name: "Information" })).toBeVisible();
    expect(screen.getByText("True Color")).toBeVisible();
  });

  it("restores remembered visibility when user preferences arrive after the gallery mounts", () => {
    useUserPreferencesStore.getState().reset();
    render(<Gallery />);
    act(() =>
      useUserPreferencesStore.getState().hydrateFromRemote({
        galleryMetadataOpen: "true",
        galleryMetadataDetailsExpanded: "true",
      }),
    );
    expect(screen.getByRole("region", { name: "Information" })).toBeVisible();
    expect(screen.getByText("True Color")).toBeVisible();
  });
  it.each(["left", "right"] as const)(
    "opens by the information button on the %s, stays open on leave, and stays closed after dismissal",
    async (position) => {
      useUserPreferencesStore
        .getState()
        .hydrateFromRemote({ galleryMetadataPosition: position });
      const { container } = render(<Gallery />);
      const button = screen.getByRole("button", { name: "Information" });
      const side = container.querySelector(
        `.media-lightbox__metadata-container--${position}`,
      );
      expect(side).not.toBeNull();
      expect(
        container.querySelector(".media-lightbox__metadata-trigger"),
      ).toBeNull();
      if (!side) {
        throw new Error("Metadata container is missing");
      }
      fireEvent.pointerEnter(side);
      expect(
        screen.queryByRole("region", { name: "Information" }),
      ).not.toBeInTheDocument();
      fireEvent.click(button);
      expect(screen.getByRole("region", { name: "Information" })).toBeVisible();
      expect(screen.getByText("Camera Model")).toBeVisible();
      expect(screen.queryByText("True Color")).not.toBeInTheDocument();
      fireEvent.pointerLeave(side);
      expect(screen.getByRole("region", { name: "Information" })).toBeVisible();
      expect(
        screen.queryByRole("button", { name: "Close" }),
      ).not.toBeInTheDocument();
      button.focus();
      fireEvent.click(button);
      fireEvent.pointerEnter(side);
      expect(
        screen.queryByRole("region", { name: "Information" }),
      ).not.toBeInTheDocument();
      expect(button).toHaveFocus();
      await waitFor(() =>
        expect(
          useUserPreferencesStore.getState().preferences.galleryMetadataOpen,
        ).toBe("false"),
      );
    },
  );

  it("shows technical tags only after expanding details and saves that choice", async () => {
    render(<Gallery />);
    fireEvent.click(screen.getByRole("button", { name: "Information" }));
    expect(screen.getByText("539 × 922")).toBeVisible();
    expect(screen.getByText("PNG")).toBeVisible();
    expect(screen.queryByText("True Color")).not.toBeInTheDocument();
    fireEvent.click(screen.getByRole("button", { name: "More details" }));
    expect(screen.getByText("True Color")).toBeVisible();
    fireEvent.click(screen.getByRole("button", { name: "Fewer details" }));
    await waitFor(() =>
      expect(screen.queryByText("True Color")).not.toBeInTheDocument(),
    );
    expect(
      useUserPreferencesStore.getState().preferences
        .galleryMetadataDetailsExpanded,
    ).toBe("false");
  });

  it("remembers the panel and details when reopening the gallery and changing slides", () => {
    const { rerender } = render(<Gallery />);
    fireEvent.click(screen.getByRole("button", { name: "Information" }));
    fireEvent.click(screen.getByRole("button", { name: "More details" }));
    rerender(<Gallery open={false} />);
    rerender(<Gallery />);
    expect(screen.getByRole("region", { name: "Information" })).toBeVisible();
    expect(screen.getByText("True Color")).toBeVisible();
    rerender(
      <Gallery item={{ ...photo, name: "next.png", metadata: undefined }} />,
    );
    expect(screen.getByText("next.png")).toBeVisible();
    expect(screen.queryByText("Camera Model")).not.toBeInTheDocument();
    expect(
      screen.queryByRole("button", { name: "Fewer details" }),
    ).not.toBeInTheDocument();
  });

  it("restores saved panel and details preferences and changes sides without closing", () => {
    useUserPreferencesStore.getState().hydrateFromRemote({
      galleryMetadataOpen: "true",
      galleryMetadataDetailsExpanded: "true",
    });
    const { container } = render(<Gallery />);
    expect(screen.getByText("True Color")).toBeVisible();
    act(() =>
      useUserPreferencesStore.getState().hydrateFromRemote({
        galleryMetadataOpen: "true",
        galleryMetadataDetailsExpanded: "true",
        galleryMetadataPosition: "left",
      }),
    );
    expect(
      container.querySelector(".media-lightbox__metadata-container--left"),
    ).toBeInTheDocument();
    expect(screen.getByRole("region", { name: "Information" })).toBeVisible();
  });

  it("opens a modal on touch screens and closes it with Escape without closing the gallery", () => {
    const { container } = render(<Gallery touch />);
    const button = screen.getByRole("button", { name: "Information" });
    fireEvent.click(button);
    expect(
      container.querySelector(".media-lightbox__metadata-container"),
    ).toBeNull();
    expect(screen.getByRole("dialog", { name: "Information" })).toBeVisible();
    fireEvent.click(screen.getByRole("button", { name: "Close" }));
    expect(button).toHaveAttribute("aria-expanded", "false");
    fireEvent.click(button);
    fireEvent.keyDown(screen.getByRole("dialog", { name: "Information" }), {
      key: "Escape",
    });
    expect(button).toHaveAttribute("aria-expanded", "false");
    expect(button).toHaveFocus();
  });

  it("closes the desktop panel with Escape from the information button", () => {
    render(<Gallery />);
    const button = screen.getByRole("button", { name: "Information" });
    fireEvent.click(button);
    fireEvent.keyDown(button, { key: "Escape" });
    expect(button).toHaveAttribute("aria-expanded", "false");
    expect(button).toHaveFocus();
  });
});
