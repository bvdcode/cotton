import { render, screen, waitFor } from "@testing-library/react";
import { useEffect } from "react";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { useAuthStore } from "../../shared/store";
import { AuthProvider } from "./AuthProvider";
import { markOidcSignInPending } from "./oidcSignInSession";
import { useAuth } from "./useAuth";
import { UserRole, type User } from "./types";

const authApiMocks = vi.hoisted(() => ({
  refresh: vi.fn(),
  me: vi.fn(),
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
    isAuthenticated: false,
    isInitializing: false,
    refreshEnabled: false,
    hydrated: true,
    hasChecked: false,
  });
};

const getWindowStorage = (
  key: "localStorage" | "sessionStorage",
): Storage | undefined => {
  try {
    return window[key];
  } catch {
    return undefined;
  }
};

const clearStorage = (key: "localStorage" | "sessionStorage"): void => {
  try {
    getWindowStorage(key)?.clear();
  } catch {
    // jsdom can expose storage as unavailable when the test origin is opaque.
  }
};

const AuthProbe = () => {
  const { ensureAuth, isAuthenticated, user: currentUser } = useAuth();

  useEffect(() => {
    void ensureAuth();
  }, [ensureAuth]);

  return (
    <div data-testid="auth-state">
      {isAuthenticated ? currentUser?.username : "anonymous"}
    </div>
  );
};

describe("AuthProvider OIDC restore", () => {
  beforeEach(() => {
    clearStorage("localStorage");
    clearStorage("sessionStorage");
    resetAuthStore();
    authApiMocks.refresh.mockReset();
    authApiMocks.me.mockReset();
    authApiMocks.logout.mockReset();
  });

  afterEach(() => {
    clearStorage("localStorage");
    clearStorage("sessionStorage");
    resetAuthStore();
  });

  it("allows refresh after an OIDC redirect even when silent refresh was disabled", async () => {
    markOidcSignInPending();
    authApiMocks.refresh.mockResolvedValue("access-token");
    authApiMocks.me.mockResolvedValue(user);

    render(
      <AuthProvider>
        <AuthProbe />
      </AuthProvider>,
    );

    await waitFor(() => {
      expect(authApiMocks.refresh).toHaveBeenCalledWith({
        allowWhenRefreshDisabled: true,
      });
    });

    await waitFor(() => {
      expect(screen.getByTestId("auth-state")).toHaveTextContent("alice");
    });
  });
});
