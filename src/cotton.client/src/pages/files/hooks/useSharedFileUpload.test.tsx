import { StrictMode, type ReactNode } from "react";
import { act, renderHook, waitFor } from "@testing-library/react";
import { MemoryRouter, useLocation } from "react-router-dom";
import { beforeEach, describe, expect, it, vi } from "vitest";
import { useVault } from "@shared/crypto/vault";
import { useSharedFileUpload } from "./useSharedFileUpload";

const mocks = vi.hoisted(() => ({ receive: vi.fn(), error: vi.fn() }));
vi.mock("@shared/pwa/sharedFiles", () => ({
  uploadSharedFiles: mocks.receive,
}));
vi.mock("@shared/ui/notifications", () => ({ toast: { error: mocks.error } }));
vi.mock("react-i18next", () => ({
  useTranslation: () => ({ t: (key: string) => key }),
}));

const upload = vi.fn<(files: File[]) => Promise<void>>(async () => {});
const wrapper = ({ children }: { children: ReactNode }) => (
  <StrictMode>
    <MemoryRouter initialEntries={["/files?sharedFiles=batch&keep=yes"]}>
      {children}
    </MemoryRouter>
  </StrictMode>
);
const renderShare = (ready = true, encryptionRequired = false) =>
  renderHook(
    (options) => {
      useSharedFileUpload({ ...options, upload });
      return useLocation();
    },
    { initialProps: { ready, encryptionRequired }, wrapper },
  );

describe("useSharedFileUpload", () => {
  beforeEach(() => {
    vi.clearAllMocks();
    useVault.setState({ isUnlocked: false, masterKey: null });
    mocks.receive.mockResolvedValue(true);
  });

  it("waits for the root folder, imports once in StrictMode and removes only its query", async () => {
    const { result, rerender } = renderShare(false);
    expect(mocks.receive).not.toHaveBeenCalled();
    rerender({ ready: true, encryptionRequired: false });
    await waitFor(() => expect(result.current.search).toBe("?keep=yes"));
    expect(mocks.receive).toHaveBeenCalledExactlyOnceWith("batch", upload);
  });

  it("waits for the vault before handing files to an encrypted root", async () => {
    const { result } = renderShare(true, true);
    expect(mocks.receive).not.toHaveBeenCalled();
    act(() => {
      useVault.setState({ isUnlocked: true });
    });
    await waitFor(() => expect(result.current.search).toBe("?keep=yes"));
    expect(mocks.receive).toHaveBeenCalledOnce();
  });

  it("shows an error when the pending share is missing", async () => {
    mocks.receive.mockResolvedValue(false);
    const { result } = renderShare();
    await waitFor(() => expect(result.current.search).toBe("?keep=yes"));
    expect(mocks.error).toHaveBeenCalledExactlyOnceWith(
      "shareTarget.receiveFailed",
    );
  });

  it("keeps the share URL after a failed upload so reloading can retry", async () => {
    mocks.receive.mockRejectedValue(new Error("offline"));
    const { result } = renderShare();
    await waitFor(() =>
      expect(mocks.error).toHaveBeenCalledWith("shareTarget.uploadFailed"),
    );
    expect(result.current.search).toBe("?sharedFiles=batch&keep=yes");
    expect(mocks.receive).toHaveBeenCalledOnce();
  });
});
