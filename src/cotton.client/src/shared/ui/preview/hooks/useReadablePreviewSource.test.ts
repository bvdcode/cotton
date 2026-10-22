import { act, cleanup, renderHook, waitFor } from "@testing-library/react";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { createFile } from "../../../../test/fileFixtures";
import {
  getReadableFileUrl,
  type ReadableFileHandle,
} from "../../../crypto/downloadDecrypt";
import { generateMasterKey } from "../../../crypto/keys";
import { useVault } from "../../../crypto/vault";
import { useReadablePreviewSource } from "./useReadablePreviewSource";

vi.mock("../../../crypto/downloadDecrypt", () => ({
  getReadableFileUrl: vi.fn(),
}));
vi.mock("../../../utils/clientDiagnostics", () => ({
  reportClientError: vi.fn(),
}));
const load = vi.mocked(getReadableFileUrl);
const file = createFile({
  name: "photo.jpg",
  contentType: "image/jpeg",
  metadata: { isClientEncrypted: "true" },
});
const handle = (): ReadableFileHandle => ({
  url: "blob:photo",
  mimeType: "image/jpeg",
  revoke: vi.fn(),
});

beforeEach(async () => {
  load.mockReset();
  useVault
    .getState()
    .unlock(await generateMasterKey(), { persistToSession: false });
});
afterEach(() => {
  cleanup();
  useVault.getState().lock();
});

describe("encrypted preview source", () => {
  it("loads the readable file once and releases it on unmount", async () => {
    const readable = handle();
    load.mockResolvedValue(readable);
    const { result, rerender, unmount } = renderHook(() =>
      useReadablePreviewSource(file),
    );
    expect(result.current.url).toBeNull();
    await waitFor(() => expect(result.current.url).toBe(readable.url));
    rerender();
    expect(load).toHaveBeenCalledExactlyOnceWith(file);
    unmount();
    expect(readable.revoke).toHaveBeenCalledOnce();
  });

  it("does not fetch while locked", () => {
    useVault.getState().lock();
    const { result } = renderHook(() => useReadablePreviewSource(file));
    expect(result.current.failed).toBe(true);
    expect(load).not.toHaveBeenCalled();
  });

  it("releases a result arriving after the preview closes", async () => {
    const readable = handle();
    let finish: (value: ReadableFileHandle) => void = () => {};
    load.mockReturnValue(
      new Promise((resolve) => {
        finish = resolve;
      }),
    );
    const { unmount } = renderHook(() => useReadablePreviewSource(file));
    unmount();
    await act(async () => {
      finish(readable);
    });
    expect(readable.revoke).toHaveBeenCalledOnce();
  });

  it("hides and releases the plaintext when the vault locks", async () => {
    const readable = handle();
    load.mockResolvedValue(readable);
    const { result } = renderHook(() => useReadablePreviewSource(file));
    await waitFor(() => expect(result.current.url).toBe(readable.url));
    act(() => useVault.getState().lock());
    expect(result.current.url).toBeNull();
    expect(result.current.failed).toBe(true);
    expect(readable.revoke).toHaveBeenCalledOnce();
  });

  it("never exposes a result decrypted after a vault lock", async () => {
    const readable = handle();
    let finish: (value: ReadableFileHandle) => void = () => {};
    load.mockReturnValue(
      new Promise((resolve) => {
        finish = resolve;
      }),
    );
    const { result } = renderHook(() => useReadablePreviewSource(file));
    act(() => useVault.getState().lock());
    await act(async () => {
      finish(readable);
    });
    expect(result.current.url).toBeNull();
    expect(readable.revoke).toHaveBeenCalledOnce();
  });

  it("does not reuse the previous file's plaintext", async () => {
    const readable = handle();
    load
      .mockResolvedValueOnce(readable)
      .mockReturnValueOnce(new Promise(() => {}));
    const { result, rerender } = renderHook(
      ({ source }) => useReadablePreviewSource(source),
      { initialProps: { source: file } },
    );
    await waitFor(() => expect(result.current.url).toBe(readable.url));
    rerender({ source: createFile({ id: "other-file" }) });
    expect(result.current.url).toBeNull();
    expect(readable.revoke).toHaveBeenCalledOnce();
  });

  it("reports decryption failure without returning a preview URL", async () => {
    load.mockRejectedValue(new Error("Invalid authentication tag"));
    const { result } = renderHook(() => useReadablePreviewSource(file));
    await waitFor(() => expect(result.current.failed).toBe(true));
    expect(result.current.url).toBeNull();
  });

  it("allows the viewer to handle unsupported media data", async () => {
    load.mockResolvedValue(handle());
    const { result } = renderHook(() => useReadablePreviewSource(file));
    await waitFor(() => expect(result.current.url).not.toBeNull());
    act(() => result.current.onSourceError());
    expect(result.current.failed).toBe(true);
    expect(result.current.url).toBeNull();
  });
});
