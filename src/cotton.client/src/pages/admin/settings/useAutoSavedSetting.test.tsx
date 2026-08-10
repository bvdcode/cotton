import { act, renderHook, waitFor } from "@testing-library/react";
import { beforeEach, describe, expect, it, vi } from "vitest";
import { useAutoSavedSetting } from "./useAutoSavedSetting";

const notifications = vi.hoisted(() => ({
  error: vi.fn(),
}));

vi.mock("@shared/ui/notifications", () => ({
  toast: notifications,
}));

vi.mock("../../../shared/api/httpClient", () => ({
  showApiErrorToast: vi.fn(),
}));

describe("useAutoSavedSetting", () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it("keeps the initial value read-only when loading fails", async () => {
    const load = vi.fn().mockRejectedValue(new Error("load failed"));
    const save = vi.fn().mockResolvedValue(undefined);
    const { result } = renderHook(() =>
      useAutoSavedSetting({
        initial: "initial",
        load,
        save,
        toastIdPrefix: "test-setting",
        loadErrorMessage: "load failed",
        saveErrorMessage: "save failed",
      }),
    );

    await waitFor(() => expect(result.current.loadFailed).toBe(true));

    expect(result.current.status).toBe("error");
    act(() => {
      result.current.setValue("edited");
      result.current.commitValue("edited");
      result.current.commit();
    });

    expect(result.current.value).toBe("initial");
    expect(save).not.toHaveBeenCalled();
  });

  it("persists changes after loading succeeds", async () => {
    const load = vi.fn().mockResolvedValue("stored");
    const save = vi.fn().mockResolvedValue(undefined);
    const { result } = renderHook(() =>
      useAutoSavedSetting({
        initial: "initial",
        load,
        save,
        toastIdPrefix: "test-setting",
        loadErrorMessage: "load failed",
        saveErrorMessage: "save failed",
      }),
    );

    await waitFor(() => expect(result.current.status).toBe("idle"));

    act(() => result.current.commitValue("updated"));

    await waitFor(() => expect(save).toHaveBeenCalledWith("updated"));
    expect(result.current.value).toBe("updated");
    expect(result.current.loadFailed).toBe(false);
  });
});
