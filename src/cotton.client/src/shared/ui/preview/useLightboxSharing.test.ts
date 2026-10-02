import { act, renderHook } from "@testing-library/react";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import type { SlideWithTitle } from "@shared/types/mediaLightbox";
import { filesApi } from "@shared/api/filesApi";
import { useUserPreferencesStore } from "@shared/store/userPreferencesStore";
import { shareFile } from "@shared/utils/shareFile";
import { shareLinkAction } from "@shared/utils/shareLinkAction";
import i18n from "../../../i18n";
import { useLightboxSharing } from "./useLightboxSharing";

vi.mock("@shared/api/filesApi", () => ({
  filesApi: { getDownloadLink: vi.fn() },
}));
vi.mock("@shared/ui/notifications", () => ({
  toast: { error: vi.fn(), success: vi.fn() },
}));

const slide: SlideWithTitle = {
  type: "image",
  src: "/preview.webp",
  fileId: "photo-id",
  fileName: "photo.CR3",
};

describe("gallery sharing", () => {
  beforeEach(() => {
    useUserPreferencesStore.getState().reset();
    vi.mocked(filesApi.getDownloadLink).mockReset();
    vi.mocked(filesApi.getDownloadLink).mockResolvedValue(
      "/api/v1/files/photo-id/download?token=year-share",
    );
    vi.stubGlobal("navigator", { share: vi.fn().mockResolvedValue(undefined) });
  });

  afterEach(() => {
    useUserPreferencesStore.getState().reset();
    vi.unstubAllGlobals();
  });

  it.each([1440, 10080, 525600])(
    "creates a share with the selected %i-minute lifetime instead of a download link",
    async (minutes) => {
      useUserPreferencesStore.getState().hydrateFromRemote({
        shareLinkExpireAfterMinutes: String(minutes),
      });
      const download = vi
        .fn()
        .mockResolvedValue(
          "/api/v1/files/photo-id/download?token=one-day-download",
        );
      const { result } = renderHook(() =>
        useLightboxSharing(download, (id, name) =>
          shareFile(id, name, i18n.getFixedT("en", "files")),
        ),
      );

      await act(() => result.current.handleCustomShare({ slide }));

      expect(filesApi.getDownloadLink).toHaveBeenCalledExactlyOnceWith(
        slide.fileId,
        minutes,
      );
      expect(navigator.share).toHaveBeenCalledWith(
        expect.objectContaining({
          title: slide.fileName,
          url: `${window.location.origin}/s/year-share`,
        }),
      );
      expect(download).not.toHaveBeenCalled();
    },
  );

  it("reads the current preference for each share while the gallery stays open", async () => {
    const { result } = renderHook(() =>
      useLightboxSharing(vi.fn(), (id, name) =>
        shareFile(id, name, i18n.getFixedT("en", "files")),
      ),
    );
    useUserPreferencesStore.getState().hydrateFromRemote({
      shareLinkExpireAfterMinutes: "1440",
    });
    await act(() => result.current.handleCustomShare({ slide }));

    useUserPreferencesStore.getState().hydrateFromRemote({
      shareLinkExpireAfterMinutes: "525600",
    });
    await act(() => result.current.handleCustomShare({ slide }));

    expect(filesApi.getDownloadLink).toHaveBeenNthCalledWith(
      1,
      slide.fileId,
      1440,
    );
    expect(filesApi.getDownloadLink).toHaveBeenNthCalledWith(
      2,
      slide.fileId,
      525600,
    );
  });

  it("re-shares public access without creating a new token or using a child download URL", async () => {
    const download = vi
      .fn()
      .mockResolvedValue(
        "/api/v1/layouts/shared/folder-token/files/photo-id?view=download",
      );
    const { result } = renderHook(() =>
      useLightboxSharing(download, async () => {
        await shareLinkAction({
          title: "Photos",
          text: "Photos",
          url: `${window.location.origin}/s/folder-token`,
        });
      }),
    );

    await act(() => result.current.handleCustomShare({ slide }));

    expect(navigator.share).toHaveBeenCalledWith(
      expect.objectContaining({
        url: `${window.location.origin}/s/folder-token`,
      }),
    );
    expect(filesApi.getDownloadLink).not.toHaveBeenCalled();
    expect(download).not.toHaveBeenCalled();
  });

  it("downloads the original without creating a share", async () => {
    const url = "/api/v1/files/photo-id/download?token=one-day-download";
    const download = vi.fn().mockResolvedValue(url);
    const onShare = vi.fn().mockResolvedValue(undefined);
    const saveAs = vi.fn();
    const { result } = renderHook(() => useLightboxSharing(download, onShare));

    await act(() => result.current.handleCustomDownload({ slide, saveAs }));

    expect(saveAs).toHaveBeenCalledExactlyOnceWith(url, slide.fileName);
    expect(onShare).not.toHaveBeenCalled();
    expect(filesApi.getDownloadLink).not.toHaveBeenCalled();
  });
});
