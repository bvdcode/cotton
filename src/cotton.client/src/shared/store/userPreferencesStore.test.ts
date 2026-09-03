import { beforeEach, describe, expect, it, vi } from "vitest";
import type { UserPreferences } from "../api/userPreferencesApi";

const updatePreferencesMock = vi.hoisted(() =>
  vi.fn<(patch: UserPreferences) => Promise<UserPreferences>>(),
);

vi.mock("../api/userPreferencesApi", () => ({
  isSelfPreferenceUpdateToken: (token: string): boolean => token === "self",
  userPreferencesApi: {
    update: updatePreferencesMock,
  },
}));

import {
  USER_PREFERENCE_KEYS,
  useUserPreferencesStore,
} from "./userPreferencesStore";

interface Deferred<T> {
  promise: Promise<T>;
  reject: (reason: Error) => void;
  resolve: (value: T) => void;
}

const createDeferred = <T>(): Deferred<T> => {
  let resolve: (value: T) => void = () => undefined;
  let reject: (reason: Error) => void = () => undefined;
  const promise = new Promise<T>((resolvePromise, rejectPromise) => {
    resolve = resolvePromise;
    reject = (reason) => rejectPromise(reason);
  });
  return { promise, reject, resolve };
};

const initialPreferences: UserPreferences = {
  [USER_PREFERENCE_KEYS.themeMode]: "system",
};

describe("userPreferencesStore synchronization", () => {
  beforeEach(() => {
    updatePreferencesMock.mockReset();
    useUserPreferencesStore.getState().reset();
    useUserPreferencesStore.getState().hydrateFromRemote(initialPreferences);
  });

  it("serializes writes while preserving later optimistic patches", async () => {
    const firstResponse = createDeferred<UserPreferences>();
    const secondResponse = createDeferred<UserPreferences>();
    updatePreferencesMock
      .mockImplementationOnce(() => firstResponse.promise)
      .mockImplementationOnce(() => secondResponse.promise);

    const layout = '{"order":["overview"],"hidden":[]}';
    const pinnedFolders = '["00000000-0000-0000-0000-000000000001"]';
    const firstUpdate = useUserPreferencesStore.getState().updatePreferences({
      [USER_PREFERENCE_KEYS.dashboardLayout]: layout,
    });
    const secondUpdate = useUserPreferencesStore.getState().updatePreferences({
      [USER_PREFERENCE_KEYS.dashboardPinnedFolderIds]: pinnedFolders,
    });

    expect(updatePreferencesMock).toHaveBeenCalledTimes(1);
    expect(useUserPreferencesStore.getState().preferences).toEqual({
      ...initialPreferences,
      [USER_PREFERENCE_KEYS.dashboardLayout]: layout,
      [USER_PREFERENCE_KEYS.dashboardPinnedFolderIds]: pinnedFolders,
    });

    firstResponse.resolve({
      ...initialPreferences,
      [USER_PREFERENCE_KEYS.dashboardLayout]: layout,
    });
    await vi.waitFor(() => {
      expect(updatePreferencesMock).toHaveBeenCalledTimes(2);
    });
    expect(updatePreferencesMock.mock.calls[1]?.[0]).toEqual({
      [USER_PREFERENCE_KEYS.dashboardPinnedFolderIds]: pinnedFolders,
    });
    expect(useUserPreferencesStore.getState().syncing).toBe(true);

    secondResponse.resolve({
      ...initialPreferences,
      [USER_PREFERENCE_KEYS.dashboardLayout]: layout,
      [USER_PREFERENCE_KEYS.dashboardPinnedFolderIds]: pinnedFolders,
    });
    await Promise.all([firstUpdate, secondUpdate]);

    expect(useUserPreferencesStore.getState()).toMatchObject({
      preferences: {
        ...initialPreferences,
        [USER_PREFERENCE_KEYS.dashboardLayout]: layout,
        [USER_PREFERENCE_KEYS.dashboardPinnedFolderIds]: pinnedFolders,
      },
      syncing: false,
    });
  });

  it("coalesces queued changes and keeps the latest value per key", async () => {
    const firstResponse = createDeferred<UserPreferences>();
    const secondResponse = createDeferred<UserPreferences>();
    updatePreferencesMock
      .mockImplementationOnce(() => firstResponse.promise)
      .mockImplementationOnce(() => secondResponse.promise);

    const firstUpdate = useUserPreferencesStore.getState().updatePreferences({
      [USER_PREFERENCE_KEYS.themeMode]: "dark",
    });
    const secondUpdate = useUserPreferencesStore.getState().updatePreferences({
      [USER_PREFERENCE_KEYS.dashboardLayout]: "layout-1",
    });
    const thirdUpdate = useUserPreferencesStore.getState().updatePreferences({
      [USER_PREFERENCE_KEYS.dashboardLayout]: "layout-2",
    });

    firstResponse.resolve({
      [USER_PREFERENCE_KEYS.themeMode]: "dark",
    });
    await vi.waitFor(() => {
      expect(updatePreferencesMock).toHaveBeenCalledTimes(2);
    });
    expect(updatePreferencesMock.mock.calls[1]?.[0]).toEqual({
      [USER_PREFERENCE_KEYS.dashboardLayout]: "layout-2",
    });

    secondResponse.resolve({
      [USER_PREFERENCE_KEYS.themeMode]: "dark",
      [USER_PREFERENCE_KEYS.dashboardLayout]: "layout-2",
    });
    await Promise.all([firstUpdate, secondUpdate, thirdUpdate]);

    expect(useUserPreferencesStore.getState().preferences).toEqual({
      [USER_PREFERENCE_KEYS.themeMode]: "dark",
      [USER_PREFERENCE_KEYS.dashboardLayout]: "layout-2",
    });
  });

  it("rolls back only the failed batch and continues with queued changes", async () => {
    const firstResponse = createDeferred<UserPreferences>();
    const secondResponse = createDeferred<UserPreferences>();
    updatePreferencesMock
      .mockImplementationOnce(() => firstResponse.promise)
      .mockImplementationOnce(() => secondResponse.promise);

    const layout = '{"order":["overview"],"hidden":[]}';
    const pinnedFolders = '["00000000-0000-0000-0000-000000000001"]';
    const firstUpdate = useUserPreferencesStore.getState().updatePreferences({
      [USER_PREFERENCE_KEYS.dashboardLayout]: layout,
    });
    const secondUpdate = useUserPreferencesStore.getState().updatePreferences({
      [USER_PREFERENCE_KEYS.dashboardPinnedFolderIds]: pinnedFolders,
    });

    firstResponse.reject(new Error("request failed"));
    await vi.waitFor(() => {
      expect(updatePreferencesMock).toHaveBeenCalledTimes(2);
    });
    expect(useUserPreferencesStore.getState().preferences).toEqual({
      ...initialPreferences,
      [USER_PREFERENCE_KEYS.dashboardPinnedFolderIds]: pinnedFolders,
    });

    secondResponse.resolve({
      ...initialPreferences,
      [USER_PREFERENCE_KEYS.dashboardPinnedFolderIds]: pinnedFolders,
    });
    await Promise.all([firstUpdate, secondUpdate]);

    expect(useUserPreferencesStore.getState()).toMatchObject({
      preferences: {
        ...initialPreferences,
        [USER_PREFERENCE_KEYS.dashboardPinnedFolderIds]: pinnedFolders,
      },
      syncing: false,
    });
  });

  it("ignores a stale response after the store is reset", async () => {
    const response = createDeferred<UserPreferences>();
    updatePreferencesMock.mockImplementationOnce(() => response.promise);

    const update = useUserPreferencesStore.getState().updatePreferences({
      [USER_PREFERENCE_KEYS.dashboardLayout]: "layout",
    });
    useUserPreferencesStore.getState().reset();
    response.resolve({
      [USER_PREFERENCE_KEYS.dashboardLayout]: "layout",
    });
    await update;

    expect(useUserPreferencesStore.getState()).toMatchObject({
      preferences: {},
      loaded: false,
      syncing: false,
    });
  });

  it("skips empty patches without entering the sync state", async () => {
    await useUserPreferencesStore.getState().updatePreferences({});

    expect(updatePreferencesMock).not.toHaveBeenCalled();
    expect(useUserPreferencesStore.getState().syncing).toBe(false);
  });
});
