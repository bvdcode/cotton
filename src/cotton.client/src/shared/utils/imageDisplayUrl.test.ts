import { describe, expect, it } from "vitest";
import { resolveImageDisplayUrl } from "./imageDisplayUrl";

describe("resolveImageDisplayUrl", () => {
  it.each([
    "cr2",
    "CR3",
    "nef",
    "nrw",
    "arw",
    "dng",
    "raf",
    "orf",
    "rw2",
    "pef",
    "srw",
  ])(
    "uses a generated preview for %s even when originals are preferred",
    (extension) => {
      expect(
        resolveImageDisplayUrl(
          "/s/photo?view=inline",
          false,
          `photo.${extension}`,
        ),
      ).toBe("/s/photo?view=inline&preview=true");
    },
  );

  it("recognizes RAW by MIME type for files without an extension", () => {
    expect(
      resolveImageDisplayUrl(
        "/s/photo?view=inline",
        false,
        "photo",
        "image/x-canon-cr3",
      ),
    ).toBe("/s/photo?view=inline&preview=true");
  });

  it.each([true, false])(
    "honors the preview preference for a browser-readable image (%s)",
    (preferPreview) => {
      const result = resolveImageDisplayUrl(
        "https://example.test/photo?token=abc&preview=true#image",
        preferPreview,
        "photo.jpg",
      );
      expect(result).toBe(
        `https://example.test/photo?token=abc&preview=${String(preferPreview)}#image`,
      );
    },
  );

  it.each(["blob:https://example.test/id", "data:image/png;base64,aA=="])(
    "preserves a local image source: %s",
    (url) => {
      expect(resolveImageDisplayUrl(url, true, "photo.jpg")).toBe(url);
    },
  );
});
