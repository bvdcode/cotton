import type { ReactNode } from "react";
import { act, cleanup, renderHook } from "@testing-library/react";
import { MemoryRouter, useLocation } from "react-router-dom";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { UserRole } from "@features/auth/types";
import { useAuthStore } from "@shared/store/authStore";
import type { showActionToast } from "@shared/ui/ActionToast";
import { useSmartSearchSetupNotification } from "./useSmartSearchSetupNotification";

const mocks = vi.hoisted(() => ({
  action: vi.fn<typeof showActionToast>(),
  info: vi.fn(),
  t: (key: string) => key,
}));

vi.mock("react-i18next", () => ({
  useTranslation: () => ({ t: mocks.t }),
}));
vi.mock("@shared/ui/ActionToast", () => ({ showActionToast: mocks.action }));
vi.mock("@shared/ui/notifications", () => ({
  toast: { info: mocks.info },
}));

const wrapper = ({ children }: { children: ReactNode }) => (
  <MemoryRouter initialEntries={["/search"]}>{children}</MemoryRouter>
);

const setUser = (role: UserRole) => {
  useAuthStore.setState({
    user: {
      id: "user-1",
      username: "user",
      role,
      createdAt: "2026-01-01T00:00:00Z",
      updatedAt: "2026-01-01T00:00:00Z",
    },
  });
};

describe("smart search setup notification", () => {
  beforeEach(() => {
    vi.clearAllMocks();
    useAuthStore.setState({ user: null });
  });

  afterEach(() => {
    cleanup();
    useAuthStore.setState({ user: null });
  });

  it("lets administrators close the search and open its configuration", () => {
    setUser(UserRole.Admin);
    const onConfigure = vi.fn();
    const { result } = renderHook(
      () => ({
        required: useSmartSearchSetupNotification(
          "smartSearch.notConfigured",
          onConfigure,
        ),
        path: useLocation().pathname,
      }),
      { wrapper },
    );

    expect(result.current.required).toBe(true);
    expect(mocks.action).toHaveBeenCalledWith(
      expect.objectContaining({
        message: "smartSearch.notConfigured",
        action: "smartSearch.configure",
        type: "info",
      }),
    );
    act(() => mocks.action.mock.calls[0][0].onAction());
    expect(onConfigure).toHaveBeenCalledOnce();
    expect(result.current.path).toBe("/admin/smart-search");
  });

  it("asks ordinary users to contact their administrator", () => {
    setUser(UserRole.User);
    renderHook(
      () => useSmartSearchSetupNotification("smartSearch.notConfigured"),
      { wrapper },
    );

    expect(mocks.info).toHaveBeenCalledWith(
      "smartSearch.contactAdministrator",
      expect.any(Object),
    );
    expect(mocks.action).not.toHaveBeenCalled();
  });

  it.each([null, "error", "smartSearch.error"])("ignores %s", (error) => {
    setUser(UserRole.Admin);
    const { result } = renderHook(
      () => useSmartSearchSetupNotification(error),
      { wrapper },
    );

    expect(result.current).toBe(false);
    expect(mocks.action).not.toHaveBeenCalled();
    expect(mocks.info).not.toHaveBeenCalled();
  });
});
