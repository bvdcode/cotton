import { render, screen, waitFor } from "@testing-library/react";
import { useEffect } from "react";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";

const storageMocks = vi.hoisted(() => {
  const createMemoryStorage = (): Storage => {
    const values = new Map<string, string>();

    return {
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
  };

  const localStorage = createMemoryStorage();
  const sessionStorage = createMemoryStorage();

  Object.defineProperty(globalThis, "localStorage", {
    value: localStorage,
    configurable: true,
  });
  Object.defineProperty(globalThis, "sessionStorage", {
    value: sessionStorage,
    configurable: true,
  });

  return { localStorage, sessionStorage };
});

import { useAuthStore } from "../../shared/store";
import { AuthProvider } from "./AuthProvider";
import { markOidcSignInPending } from "./oidcSignInSession";
import { useAuth } from "./useAuth";
import { UserRole, type User } from "./types";

const authApiMocks = vi.hoisted(() => ({
  restoreSession: vi.fn(),
  logout: vi.fn(),
}));

vi.mock("../../shared/api/authApi", () => ({
  authApi: authApiMocks,
}));

vi.mock("../../shared/store/resetUserScopedStores", () => ({
  resetUserScopedStores: vi.fn(),
}));

const user: User = {
  id: "user-1",
  role: UserRole.User,
  username: "alice",
  email: "alice@example.com",
  isEmailVerified: true,
  displayName: "Alice",
  createdAt: "2026-05-28T00:00:00Z",
  updatedAt: "2026-05-28T00:00:01Z",
};

const resetAuthStore = () => {
  useAuthStore.setState({
    user: null,
    phase: "booting",
    refreshEnabled: false,
  });
};

const AuthProbe = () => {
  const {
    restoreSession,
    isAuthenticated,
    phase,
    user: currentUser,
  } = useAuth();

  useEffect(() => {
    void restoreSession();
  }, [restoreSession]);

  return (
    <div data-testid="auth-state">
      {phase}:{isAuthenticated ? currentUser?.username : "anonymous"}
    </div>
  );
};

const DoubleRestoreProbe = () => {
  const { restoreSession } = useAuth();

  useEffect(() => {
    void Promise.all([restoreSession(), restoreSession()]);
  }, [restoreSession]);

  return null;
};

describe("AuthProvider OIDC restore", () => {
  beforeEach(() => {
    storageMocks.localStorage.clear();
    storageMocks.sessionStorage.clear();
    resetAuthStore();
    authApiMocks.restoreSession.mockReset();
    authApiMocks.logout.mockReset();
  });

  afterEach(() => {
    storageMocks.localStorage.clear();
    storageMocks.sessionStorage.clear();
    resetAuthStore();
  });

  it("allows refresh after an OIDC redirect even when silent refresh was disabled", async () => {
    markOidcSignInPending();
    authApiMocks.restoreSession.mockResolvedValue(user);

    render(
      <AuthProvider>
        <AuthProbe />
      </AuthProvider>,
    );

    await waitFor(() => {
      expect(authApiMocks.restoreSession).toHaveBeenCalledWith({
        allowWhenRefreshDisabled: true,
      });
    });

    await waitFor(() => {
      expect(screen.getByTestId("auth-state")).toHaveTextContent(
        "authenticated:alice",
      );
    });
  });

  it("restores a regular browser session with one request", async () => {
    useAuthStore.setState({ refreshEnabled: true });
    authApiMocks.restoreSession.mockResolvedValue(user);

    render(
      <AuthProvider>
        <AuthProbe />
      </AuthProvider>,
    );

    await waitFor(() => {
      expect(authApiMocks.restoreSession).toHaveBeenCalledTimes(1);
      expect(authApiMocks.restoreSession).toHaveBeenCalledWith({
        allowWhenRefreshDisabled: false,
      });
      expect(screen.getByTestId("auth-state")).toHaveTextContent(
        "authenticated:alice",
      );
    });
  });

  it("does not restore a session after explicit logout", async () => {
    render(
      <AuthProvider>
        <AuthProbe />
      </AuthProvider>,
    );

    await waitFor(() => {
      expect(screen.getByTestId("auth-state")).toHaveTextContent(
        "anonymous:anonymous",
      );
    });
    expect(authApiMocks.restoreSession).not.toHaveBeenCalled();
  });

  it("deduplicates concurrent restore requests", async () => {
    useAuthStore.setState({ refreshEnabled: true });
    authApiMocks.restoreSession.mockResolvedValue(user);

    render(
      <AuthProvider>
        <DoubleRestoreProbe />
      </AuthProvider>,
    );

    await waitFor(() => {
      expect(authApiMocks.restoreSession).toHaveBeenCalledTimes(1);
    });
  });
});
