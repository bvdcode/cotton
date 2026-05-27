import { beforeEach, describe, expect, it, vi } from "vitest";

const localStorageMock = vi.hoisted(() => {
  const values = new Map<string, string>();
  const storage: Storage = {
    get length() {
      return values.size;
    },
    clear: () => {
      values.clear();
    },
    getItem: (key) => values.get(key) ?? null,
    key: (index) => Array.from(values.keys())[index] ?? null,
    removeItem: (key) => {
      values.delete(key);
    },
    setItem: (key, value) => {
      values.set(key, value);
    },
  };

  Object.defineProperty(globalThis, "localStorage", {
    value: storage,
    configurable: true,
  });

  return storage;
});

import { getRefreshEnabled, useAuthStore } from "./authStore";

describe("authStore", () => {
  beforeEach(() => {
    localStorageMock.clear();
    useAuthStore.setState({
      user: null,
      isAuthenticated: false,
      isInitializing: false,
      refreshEnabled: true,
      hydrated: true,
      hasChecked: false,
    });
  });

  it("keeps refresh disabled after local logout", () => {
    useAuthStore.getState().logoutLocal();

    expect(getRefreshEnabled()).toBe(false);
  });
});
