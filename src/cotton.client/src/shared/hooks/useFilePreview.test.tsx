import { act, cleanup, renderHook } from "@testing-library/react";
import { afterEach, describe, expect, it, vi } from "vitest";
import { createFile } from "../../test/fileFixtures";
import { ENCRYPTED_FLAG_KEY } from "../crypto/fileCipher";
import { previewConfig } from "../config/previewConfig";
import { useFilePreview } from "./useFilePreview";

afterEach(() => {
  cleanup();
  vi.restoreAllMocks();
});

describe("file preview selection", () => {
  it.each([
    ["archive.zip", "application/zip", 12],
    ["song.mp3", "audio/mpeg", 12],
    ["large.txt", "text/plain", previewConfig.MAX_TEXT_PREVIEW_SIZE_BYTES + 1],
  ])(
    "opens a download choice for %s without starting a download",
    (name, contentType, sizeBytes) => {
      vi.spyOn(window.history, "pushState").mockImplementation(() => {});
      const file = createFile({ name, contentType, sizeBytes });
      const { result } = renderHook(useFilePreview);
      act(() =>
        result.current.openPreview(file.id, name, sizeBytes, contentType, file),
      );
      expect(result.current.previewState).toMatchObject({
        isOpen: true,
        fileType: "other",
        file,
      });
    },
  );

  it("keeps encrypted text readable and offers download for encrypted media", () => {
    vi.spyOn(window.history, "pushState").mockImplementation(() => {});
    const file = createFile({ metadata: { [ENCRYPTED_FLAG_KEY]: "true" } });
    const { result } = renderHook(useFilePreview);
    act(() =>
      result.current.openPreview(
        file.id,
        file.name,
        file.sizeBytes,
        file.contentType,
        file,
      ),
    );
    expect(result.current.previewState.fileType).toBe("text");
    act(() =>
      result.current.openPreview(file.id, "secret.jpg", 1, "image/jpeg", file),
    );
    expect(result.current.previewState.fileType).toBe("other");
  });
});
