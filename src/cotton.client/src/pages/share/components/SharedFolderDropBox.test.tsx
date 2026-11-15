import { cleanup, fireEvent, render, screen } from "@testing-library/react";
import { afterEach, expect, it, vi } from "vitest";
import type { SharedNodeInfoDto } from "@shared/api/sharedFoldersApi";
import { uploadManager } from "@shared/upload/UploadManager";
import { SharedFolderDropBox } from "./SharedFolderDropBox";

vi.mock("react-i18next", () => ({
  useTranslation: () => ({ t: (key: string) => key }),
}));
vi.mock("@shared/upload/UploadManager", () => ({
  uploadManager: {
    subscribe: () => () => {},
    getSnapshot: () => ({ tasks: [] }),
    enqueue: vi.fn(() => []),
  },
}));

const info: SharedNodeInfoDto = {
  token: "drop-box",
  nodeId: "folder",
  name: "",
  allowUpload: true,
  hideFolderContent: true,
  maxChunkSizeBytes: 1024,
  supportedHashAlgorithm: "SHA256",
};

afterEach(() => {
  cleanup();
  vi.clearAllMocks();
});

it.each(["page", "overlay"])(
  "queues each file once when dropped on the %s",
  (target) => {
    render(<SharedFolderDropBox info={info} />);
    const main = screen.getByRole("main");
    const files = [
      new File(["data"], "first.txt"),
      new File(["data"], "second.txt"),
    ];
    const dataTransfer = { types: ["Files"], items: [], files };
    fireEvent.dragEnter(main, { dataTransfer });
    const dropTarget =
      target === "overlay" ? screen.getByText("actions.dropFiles") : main;
    fireEvent.drop(dropTarget, { dataTransfer });
    expect(uploadManager.enqueue).toHaveBeenCalledExactlyOnceWith(
      files.map((file) => ({ file })),
      info.nodeId,
      "",
      expect.objectContaining({
        sharedUpload: expect.objectContaining({ token: info.token }),
      }),
    );
    expect(screen.queryByText("actions.dropFiles")).toBeNull();
  },
);
