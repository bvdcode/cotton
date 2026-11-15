import { act, cleanup, renderHook } from "@testing-library/react";
import { afterEach, expect, it, vi } from "vitest";
import {
  sharedFoldersApi,
  type SharedNodeInfoDto,
} from "@shared/api/sharedFoldersApi";
import { uploadManager } from "@shared/upload/UploadManager";
import { useSharedFolderUpload } from "./useSharedFolderUpload";

vi.mock("react-i18next", () => ({
  useTranslation: () => ({ t: (key: string) => key }),
}));
vi.mock("@shared/upload/UploadManager", () => ({
  uploadManager: {
    openFilePicker: vi.fn(),
    enqueue: vi.fn(() => ["upload-1"]),
  },
}));

const info: SharedNodeInfoDto = {
  token: "drop-box",
  nodeId: "folder",
  name: "Uploads",
  allowUpload: true,
  hideFolderContent: true,
  maxChunkSizeBytes: 1024,
  supportedHashAlgorithm: "SHA256",
};

afterEach(() => {
  cleanup();
  vi.restoreAllMocks();
});

it("queues hidden-folder uploads without requesting the listing or resolving names", async () => {
  const read = vi.spyOn(sharedFoldersApi, "getChildren");
  const onQueued = vi.fn();
  const { result } = renderHook(() =>
    useSharedFolderUpload(info, info.nodeId, info.name, vi.fn(), onQueued),
  );
  act(() => result.current.openPicker());
  const context = vi
    .mocked(uploadManager.openFilePicker)
    .mock.calls.at(-1)?.[0];
  expect(context).toBeDefined();
  const file = new File(["data"], "already-exists.txt");
  await act(async () => context?.onFilesSelected?.([file]));
  expect(read).not.toHaveBeenCalled();
  expect(uploadManager.enqueue).toHaveBeenCalledWith(
    [{ file }],
    info.nodeId,
    info.name,
    expect.objectContaining({
      sharedUpload: expect.objectContaining({ token: info.token }),
    }),
  );
  expect(onQueued).toHaveBeenCalledWith(["upload-1"]);
});
