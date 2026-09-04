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
      phase: "booting",
      refreshEnabled: true,
    });
  });

  it("keeps refresh disabled after local logout", () => {
    useAuthStore.getState().logoutLocal();

    expect(getRefreshEnabled()).toBe(false);
    expect(useAuthStore.getState().phase).toBe("anonymous");
  });

  it("uses one phase value for authenticated and anonymous transitions", () => {
    useAuthStore.getState().setAuthenticated({
      id: "user-1",
      role: 1,
      username: "alice",
      createdAt: "2026-09-03T00:00:00Z",
      updatedAt: "2026-09-03T00:00:00Z",
    });
    expect(useAuthStore.getState().phase).toBe("authenticated");

    useAuthStore.getState().setUnauthenticated();
    expect(useAuthStore.getState().phase).toBe("anonymous");
  });

  it("represents temporary server unavailability without disabling refresh", () => {
    useAuthStore.getState().setUnavailable();

    expect(useAuthStore.getState().phase).toBe("unavailable");
    expect(getRefreshEnabled()).toBe(true);
  });

  it("updates auth state when localStorage is unavailable", () => {
    const availableStorage = globalThis.localStorage;
    Object.defineProperty(globalThis, "localStorage", {
      value: undefined,
      configurable: true,
    });

    try {
      expect(() => useAuthStore.getState().logoutLocal()).not.toThrow();
      expect(getRefreshEnabled()).toBe(false);
    } finally {
      Object.defineProperty(globalThis, "localStorage", {
        value: availableStorage,
        configurable: true,
      });
    }
  });
});
