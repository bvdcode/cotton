import { describe, expect, it } from "vitest";
import { OIDC_SIGN_IN_PENDING_STORAGE_KEY } from "./authStorageKeys";
import {
  clearOidcSignInPending,
  consumeOidcSignInPending,
  markOidcSignInPending,
} from "./oidcSignInSession";

const createStorage = (): Pick<Storage, "getItem" | "removeItem" | "setItem"> => {
  const values = new Map<string, string>();

  return {
    getItem: (key) => values.get(key) ?? null,
    removeItem: (key) => {
      values.delete(key);
    },
    setItem: (key, value) => {
      values.set(key, value);
    },
  };
};

const createLocation = (
  pathname: string,
  search = "",
): Pick<Location, "pathname" | "search"> => ({ pathname, search });

describe("oidcSignInSession", () => {
  it("consumes a pending marker once on protected returns", () => {
    const storage = createStorage();

    markOidcSignInPending(storage);

    expect(consumeOidcSignInPending(createLocation("/settings"), storage)).toBe(true);
    expect(consumeOidcSignInPending(createLocation("/settings"), storage)).toBe(false);
  });

  it("clears pending state on login returns", () => {
    const storage = createStorage();

    markOidcSignInPending(storage);

    expect(consumeOidcSignInPending(createLocation("/login"), storage)).toBe(false);
    expect(storage.getItem(OIDC_SIGN_IN_PENDING_STORAGE_KEY)).toBeNull();
  });

  it("clears pending state on cancelled provider returns", () => {
    const storage = createStorage();

    markOidcSignInPending(storage);

    expect(
      consumeOidcSignInPending(createLocation("/login", "?oidc=cancelled"), storage),
    ).toBe(false);
    expect(storage.getItem(OIDC_SIGN_IN_PENDING_STORAGE_KEY)).toBeNull();
  });

  it("ignores blocked session storage", () => {
    expect(() => {
      markOidcSignInPending(null);
      clearOidcSignInPending(null);
      consumeOidcSignInPending(createLocation("/settings"), null);
    }).not.toThrow();
  });
});
