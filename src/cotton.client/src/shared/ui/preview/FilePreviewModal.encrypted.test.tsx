import { act, cleanup, render, screen, waitFor } from "@testing-library/react";
import { afterEach, beforeEach, expect, it, vi } from "vitest";
import { createFile } from "../../../test/fileFixtures";
import type { FileType } from "../../utils/fileTypes";
import { generateMasterKey } from "../../crypto/keys";
import { useVault } from "../../crypto/vault";
import {
  getReadableFileUrl,
  type ReadableFileHandle,
} from "../../crypto/downloadDecrypt";
import { FilePreviewModal } from "./FilePreviewModal";

const viewers = vi.hoisted(() => ({
  pdf: vi.fn(),
  model: vi.fn(),
  text: vi.fn(),
  audio: vi.fn(),
}));
vi.mock("react-i18next", () => ({
  useTranslation: () => ({ t: (key: string) => key }),
}));
vi.mock("../../crypto/downloadDecrypt", () => ({
  getReadableFileUrl: vi.fn(),
}));
vi.mock("../../utils/clientDiagnostics", () => ({
  reportClientError: vi.fn(),
}));
vi.mock("./PdfPreview", () => ({
  PdfPreview: (props: object) => {
    viewers.pdf(props);
    return <div>pdf viewer</div>;
  },
}));
vi.mock("./ModelPreview", () => ({
  ModelPreview: (props: object) => {
    viewers.model(props);
    return <div>model viewer</div>;
  },
}));
vi.mock("./TextPreview", () => ({
  TextPreview: (props: object) => {
    viewers.text(props);
    return <div>text viewer</div>;
  },
}));
vi.mock("./AudioPreview", () => ({
  AudioPreview: (props: object) => {
    viewers.audio(props);
    return <div>audio viewer</div>;
  },
}));
const load = vi.mocked(getReadableFileUrl);
const file = createFile({ metadata: { isClientEncrypted: "true" } });
const base = {
  fileId: file.id,
  fileName: "secret.pdf",
  fileType: "pdf" as FileType,
  fileSizeBytes: file.sizeBytes,
  file,
  onClose: vi.fn(),
  onDownload: vi.fn().mockResolvedValue(undefined),
};
const handle = (): ReadableFileHandle => ({
  url: "blob:plaintext",
  mimeType: "application/pdf",
  revoke: vi.fn(),
});

beforeEach(async () => {
  vi.clearAllMocks();
  load.mockReset();
  useVault
    .getState()
    .unlock(await generateMasterKey(), { persistToSession: false });
});
afterEach(() => {
  cleanup();
  useVault.getState().lock();
});

it("starts decryption only after opening and keeps a loading preview visible", async () => {
  const readable = handle();
  let finish: (value: ReadableFileHandle) => void = () => {};
  load.mockReturnValue(
    new Promise((resolve) => {
      finish = resolve;
    }),
  );
  const { rerender } = render(<FilePreviewModal {...base} isOpen={false} />);
  expect(load).not.toHaveBeenCalled();
  rerender(<FilePreviewModal {...base} isOpen />);
  expect(screen.getByRole("dialog")).toBeVisible();
  expect(screen.getByRole("progressbar")).toBeVisible();
  expect(viewers.pdf).not.toHaveBeenCalled();
  await act(async () => {
    finish(readable);
  });
  expect(viewers.pdf).toHaveBeenLastCalledWith(
    expect.objectContaining({
      source: { kind: "objectUrl", url: readable.url },
    }),
  );
  expect(load).toHaveBeenCalledExactlyOnceWith(file);
  rerender(<FilePreviewModal {...base} isOpen={false} />);
  expect(readable.revoke).toHaveBeenCalledOnce();
});

it.each(["pdf", "model", "text", "audio"] as const)(
  "passes one decrypted source to the %s viewer and removes it on lock",
  async (type) => {
    const readable = handle();
    load.mockResolvedValue(readable);
    render(<FilePreviewModal {...base} isOpen fileType={type} />);
    await screen.findByText(`${type} viewer`);
    let props: object;
    switch (type) {
      case "text":
        props = { previewUrl: readable.url, sourceFile: file };
        break;
      case "audio":
        props = { sourceUrl: readable.url };
        break;
      case "pdf":
        props = { source: { kind: "objectUrl", url: readable.url } };
        break;
      case "model":
        props = { source: { kind: "url", url: readable.url } };
        break;
    }
    expect(viewers[type]).toHaveBeenLastCalledWith(
      expect.objectContaining(props),
    );
    expect(load).toHaveBeenCalledExactlyOnceWith(file);
    act(() => useVault.getState().lock());
    expect(screen.queryByText(`${type} viewer`)).toBeNull();
    expect(readable.revoke).toHaveBeenCalledOnce();
  },
);

it.each(["image", "video"] as const)(
  "feeds the decrypted URL to native %s",
  async (type) => {
    const readable = handle();
    load.mockResolvedValue(readable);
    const { container, unmount } = render(
      <FilePreviewModal {...base} isOpen fileType={type} />,
    );
    let tag: string;
    switch (type) {
      case "image":
        tag = "img";
        break;
      case "video":
        tag = "video";
        break;
    }
    await waitFor(() =>
      expect(container.ownerDocument.querySelector(tag)).toHaveAttribute(
        "src",
        readable.url,
      ),
    );
    expect(load).toHaveBeenCalledExactlyOnceWith(file);
    unmount();
    expect(container.ownerDocument.querySelector(tag)).toBeNull();
    expect(readable.revoke).toHaveBeenCalledOnce();
  },
);

it("never decrypts unsupported files", () => {
  render(
    <FilePreviewModal
      {...base}
      isOpen
      fileName="secret.avi"
      fileType="other"
    />,
  );
  expect(screen.getByText("share:unsupported")).toBeVisible();
  expect(load).not.toHaveBeenCalled();
});

it("keeps ordinary PDF previews independent of the vault", () => {
  useVault.getState().lock();
  render(<FilePreviewModal {...base} isOpen file={null} />);
  expect(viewers.pdf).toHaveBeenCalledWith(
    expect.objectContaining({ source: { kind: "fileId", fileId: file.id } }),
  );
  expect(load).not.toHaveBeenCalled();
});
