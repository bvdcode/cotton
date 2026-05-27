import { OIDC_SIGN_IN_PENDING_STORAGE_KEY } from "./authStorageKeys";

type SessionStorageLike = Pick<Storage, "getItem" | "removeItem" | "setItem">;
type LocationLike = Pick<Location, "pathname" | "search">;

const getSessionStorage = (): SessionStorageLike | null => {
  if (typeof window === "undefined") {
    return null;
  }

  try {
    return window.sessionStorage;
  } catch {
    return null;
  }
};

const getLocation = (): LocationLike | null => {
  if (typeof window === "undefined") {
    return null;
  }

  return window.location;
};

export const markOidcSignInPending = (
  storage: SessionStorageLike | null = getSessionStorage(),
): void => {
  if (!storage) {
    return;
  }

  try {
    storage.setItem(OIDC_SIGN_IN_PENDING_STORAGE_KEY, "1");
  } catch {
    // Session storage can be blocked in private or embedded contexts.
  }
};

export const clearOidcSignInPending = (
  storage: SessionStorageLike | null = getSessionStorage(),
): void => {
  if (!storage) {
    return;
  }

  try {
    storage.removeItem(OIDC_SIGN_IN_PENDING_STORAGE_KEY);
  } catch {
    // Session storage can be blocked in private or embedded contexts.
  }
};

export const consumeOidcSignInPending = (
  location: LocationLike | null = getLocation(),
  storage: SessionStorageLike | null = getSessionStorage(),
): boolean => {
  if (!location || !storage) {
    return false;
  }

  const oidcStatus = new URLSearchParams(location.search).get("oidc");
  if (location.pathname === "/login" || oidcStatus === "cancelled") {
    clearOidcSignInPending(storage);
    return false;
  }

  try {
    const value = storage.getItem(OIDC_SIGN_IN_PENDING_STORAGE_KEY);
    storage.removeItem(OIDC_SIGN_IN_PENDING_STORAGE_KEY);
    return value !== null;
  } catch {
    return false;
  }
};
