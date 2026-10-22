import { act, cleanup, renderHook } from "@testing-library/react";
import { afterEach, describe, expect, it, vi } from "vitest";
import { createFile } from "../../test/fileFixtures";
import { ENCRYPTED_FLAG_KEY } from "../crypto/fileCipher";
import { CLIENT_ENCRYPTION_BLOB_PIPELINE_MAX_BYTES } from "../crypto/limits";
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

  it("keeps encrypted text readable and opens encrypted JPEG images", () => {
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
    expect(result.current.previewState.fileType).toBe("image");
  });

  it.each([
    ["secret.png", "image/png", 100, "image"],
    ["secret.webp", "image/webp", 100, "image"],
    ["secret.gif", "image/gif", 100, "image"],
    ["secret.heic", "image/heic", 100, "image"],
    ["secret.heif", "image/heif", 100, "image"],
    ["secret.cr3", "image/x-canon-cr3", 100, "other"],
    ["secret.mp4", "video/mp4", 100, "video"],
    ["secret.webm", "video/webm", 100, "video"],
    ["secret.mp3", "audio/mpeg", 100, "audio"],
    ["secret.pdf", "application/pdf", 100, "pdf"],
    ["secret.stl", "model/stl", 100, "model"],
    ["secret.glb", "model/gltf-binary", 100, "model"],
    ["secret.avi", "video/x-msvideo", 100, "other"],
    ["secret.mkv", "video/x-matroska", 100, "other"],
    [
      "large.pdf",
      "application/pdf",
      previewConfig.MAX_PDF_PREVIEW_SIZE_BYTES + 1,
      "other",
    ],
    [
      "large.mp4",
      "video/mp4",
      CLIENT_ENCRYPTION_BLOB_PIPELINE_MAX_BYTES + 1,
      "other",
    ],
    [
      "huge.jpg",
      "image/jpeg",
      CLIENT_ENCRYPTION_BLOB_PIPELINE_MAX_BYTES + 1,
      "other",
    ],
  ])(
    "selects %s within browser preview limits",
    (name, contentType, sizeBytes, expected) => {
      vi.spyOn(window.history, "pushState").mockImplementation(() => {});
      const file = createFile({
        name,
        contentType,
        sizeBytes,
        metadata: { [ENCRYPTED_FLAG_KEY]: "true" },
      });
      const { result } = renderHook(useFilePreview);
      act(() =>
        result.current.openPreview(file.id, name, sizeBytes, contentType, file),
      );
      expect(result.current.previewState.fileType).toBe(expected);
    },
  );
});
