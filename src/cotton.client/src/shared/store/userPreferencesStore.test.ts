import { beforeEach, describe, expect, it, vi } from "vitest";
import {
  userPreferencesApi,
  type UserPreferences,
} from "../api/userPreferencesApi";
import { useUserPreferencesStore } from "./userPreferencesStore";

vi.mock("../utils/clientDiagnostics", () => ({ reportClientError: vi.fn() }));

const deferred = () => {
  let resolve!: (preferences: UserPreferences) => void;
  let reject!: (error: Error) => void;
  const promise = new Promise<UserPreferences>(
    (resolvePromise, rejectPromise) => {
      resolve = resolvePromise;
      reject = rejectPromise;
    },
  );
  return { promise, resolve, reject };
};

beforeEach(() => {
  vi.restoreAllMocks();
  useUserPreferencesStore.getState().reset();
});

describe("user preference updates", () => {
  it("serializes rapid panel and details changes without losing either optimistic value", async () => {
    const panel = deferred();
    const details = deferred();
    const update = vi
      .spyOn(userPreferencesApi, "update")
      .mockReturnValueOnce(panel.promise)
      .mockReturnValueOnce(details.promise);
    const store = useUserPreferencesStore.getState();
    const first = store.updatePreferences({ galleryMetadataOpen: "true" });
    const second = store.updatePreferences({
      galleryMetadataDetailsExpanded: "true",
    });
    expect(useUserPreferencesStore.getState().preferences).toEqual({
      galleryMetadataOpen: "true",
      galleryMetadataDetailsExpanded: "true",
    });
    await Promise.resolve();
    expect(update).toHaveBeenCalledTimes(1);
    panel.resolve({ galleryMetadataOpen: "true" });
    await first;
    expect(
      useUserPreferencesStore.getState().preferences
        .galleryMetadataDetailsExpanded,
    ).toBe("true");
    expect(useUserPreferencesStore.getState().syncing).toBe(true);
    await Promise.resolve();
    expect(update).toHaveBeenCalledTimes(2);
    details.resolve({
      galleryMetadataOpen: "true",
      galleryMetadataDetailsExpanded: "true",
    });
    await second;
    expect(useUserPreferencesStore.getState().syncing).toBe(false);
    expect(useUserPreferencesStore.getState().preferences).toEqual({
      galleryMetadataOpen: "true",
      galleryMetadataDetailsExpanded: "true",
    });
  });

  it.each([true, false])(
    "rolls back to the last saved preferences when the final update fails (first succeeds: %s)",
    async (firstSucceeds) => {
      const panel = deferred();
      const details = deferred();
      vi.spyOn(userPreferencesApi, "update")
        .mockReturnValueOnce(panel.promise)
        .mockReturnValueOnce(details.promise);
      useUserPreferencesStore
        .getState()
        .hydrateFromRemote({ themeMode: "dark" });
      const first = useUserPreferencesStore
        .getState()
        .updatePreferences({ galleryMetadataOpen: "true" });
      const second = useUserPreferencesStore
        .getState()
        .updatePreferences({ galleryMetadataDetailsExpanded: "true" });
      await Promise.resolve();
      if (firstSucceeds) {
        panel.resolve({ themeMode: "dark", galleryMetadataOpen: "true" });
      } else {
        panel.reject(new Error("Panel update failed"));
      }
      await first;
      await Promise.resolve();
      details.reject(new Error("Details update failed"));
      await second;
      expect(useUserPreferencesStore.getState().preferences).toEqual(
        firstSucceeds
          ? { themeMode: "dark", galleryMetadataOpen: "true" }
          : { themeMode: "dark" },
      );
      expect(useUserPreferencesStore.getState().syncing).toBe(false);
    },
  );

  it("drops queued updates and ignores an old response when the user state resets", async () => {
    const pending = deferred();
    const update = vi
      .spyOn(userPreferencesApi, "update")
      .mockReturnValueOnce(pending.promise);
    const first = useUserPreferencesStore
      .getState()
      .updatePreferences({ galleryMetadataOpen: "true" });
    const second = useUserPreferencesStore
      .getState()
      .updatePreferences({ galleryMetadataDetailsExpanded: "true" });
    await Promise.resolve();
    useUserPreferencesStore.getState().reset();
    useUserPreferencesStore
      .getState()
      .hydrateFromRemote({ themeMode: "light" });
    pending.resolve({ galleryMetadataOpen: "true" });
    await Promise.all([first, second]);
    expect(update).toHaveBeenCalledTimes(1);
    expect(useUserPreferencesStore.getState().preferences).toEqual({
      themeMode: "light",
    });
    expect(useUserPreferencesStore.getState().syncing).toBe(false);
  });
});
