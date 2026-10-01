import { fireEvent, render, screen } from "@testing-library/react";
import { beforeEach, describe, expect, it, vi } from "vitest";
import type { MediaLightboxProps } from "@shared/types/mediaLightbox";
import { ShareMediaViewer } from "./ShareMediaViewer";

let lightboxProps: MediaLightboxProps | null = null;
let preferPreview = false;
vi.mock("@shared/ui/preview", () => ({
  MediaLightbox: (props: MediaLightboxProps) => {
    lightboxProps = props;
    return props.open ? (
      <button onClick={props.onClose}>Close gallery</button>
    ) : null;
  },
}));
vi.mock("../../../shared/store/userPreferencesStore", () => ({
  selectGalleryPreferPreview: "preview",
  selectGallerySmoothTransitions: "transitions",
  useUserPreferencesStore: (selector: string) =>
    selector === "preview" ? preferPreview : true,
}));

describe("ShareMediaViewer", () => {
  beforeEach(() => {
    lightboxProps = null;
    preferPreview = false;
  });

  it.each([true, false])(
    "shows the RAW preview after closing and reopening the gallery (preference %s)",
    async (preference) => {
      preferPreview = preference;
      render(
        <ShareMediaViewer
          token="photo"
          title="photo.CR3"
          fileName="photo.CR3"
          inlineUrl="/s/photo?view=inline"
          downloadUrl="/s/photo?view=download"
          previewUrl="/s/photo?view=inline&preview=true"
          contentType="image/x-canon-cr3"
          contentLength={1234}
        />,
      );

      expect(lightboxProps?.items[0].previewUrl).toBe(
        "/s/photo?view=inline&preview=true",
      );
      expect(await lightboxProps?.getDownloadUrl?.("photo")).toBe(
        "/s/photo?view=download",
      );
      fireEvent.click(screen.getByRole("button", { name: "Close gallery" }));
      const image = screen.getByRole("img", { name: "photo.CR3" });
      expect(image).toHaveAttribute("src", "/s/photo?view=inline&preview=true");
      fireEvent.click(image);
      expect(
        screen.getByRole("button", { name: "Close gallery" }),
      ).toBeInTheDocument();
    },
  );

  it("keeps the original JPEG when originals are preferred", () => {
    render(
      <ShareMediaViewer
        token="photo"
        title="photo.jpg"
        fileName="photo.jpg"
        inlineUrl="/s/photo?view=inline"
        downloadUrl="/s/photo?view=download"
        previewUrl="/s/photo?view=inline&preview=true"
        contentType="image/jpeg"
        contentLength={1234}
      />,
    );
    fireEvent.click(screen.getByRole("button", { name: "Close gallery" }));
    expect(screen.getByRole("img", { name: "photo.jpg" })).toHaveAttribute(
      "src",
      "/s/photo?view=inline&preview=false",
    );
  });
});
