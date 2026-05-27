import { beforeEach, describe, expect, it } from "vitest";
import { getRefreshEnabled, useAuthStore } from "./authStore";

describe("authStore", () => {
  beforeEach(() => {
    window.localStorage.clear();
    useAuthStore.setState({
      user: null,
      isAuthenticated: false,
      isInitializing: false,
      refreshEnabled: true,
      hydrated: true,
      hasChecked: false,
    });
  });

  it("can re-enable refresh after local logout for external auth redirects", () => {
    useAuthStore.getState().logoutLocal();

    expect(getRefreshEnabled()).toBe(false);

    useAuthStore.getState().enableRefresh();

    expect(getRefreshEnabled()).toBe(true);
  });
});
