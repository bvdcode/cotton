import { renderHook, waitFor } from "@testing-library/react";
import { beforeEach, describe, expect, it, vi } from "vitest";

const testState = vi.hoisted(() => ({
  resolvedFolderIds: [] as string[],
  dataUpdatedAt: 1,
  isError: false,
  isPending: false,
  isSuccess: true,
}));

const updatePreferencesApiMock = vi.hoisted(() => vi.fn());

vi.mock("../api/queries/layouts", () => ({
  usePinnedFoldersQuery: () => ({
    data: testState.resolvedFolderIds.map((id) => ({ id })),
    dataUpdatedAt: testState.dataUpdatedAt,
    isError: testState.isError,
    isPending: testState.isPending,
    isSuccess: testState.isSuccess,
    refetch: vi.fn(),
  }),
}));

vi.mock("../api/userPreferencesApi", () => ({
  isSelfPreferenceUpdateToken: () => false,
  userPreferencesApi: {
    update: updatePreferencesApiMock,
  },
}));

vi.mock("react-i18next", () => ({
  useTranslation: () => ({ t: (key: string) => key }),
}));

vi.mock("../ui/notifications", () => ({
  toast: { error: vi.fn() },
}));

import {
  USER_PREFERENCE_KEYS,
  useUserPreferencesStore,
} from "../store/userPreferencesStore";
import { usePinnedFolders } from "./usePinnedFolders";

const folderId = (index: number): string =>
  `00000000-0000-7000-8000-${index.toString().padStart(12, "0")}`;

const hydratePinnedFolders = (folderIds: readonly string[]): void => {
  useUserPreferencesStore.getState().hydrateFromRemote({
    [USER_PREFERENCE_KEYS.dashboardPinnedFolderIds]: JSON.stringify(folderIds),
  });
};

describe("usePinnedFolders", () => {
  beforeEach(() => {
    testState.resolvedFolderIds = [];
    testState.dataUpdatedAt = 1;
    testState.isError = false;
    testState.isPending = false;
    testState.isSuccess = true;
    updatePreferencesApiMock.mockReset();
    useUserPreferencesStore.getState().reset();
  });

  it("removes ids omitted by a successful folder resolve", async () => {
    const existing = folderId(1);
    const missing = folderId(2);
    const serializedExisting = JSON.stringify([existing]);
    hydratePinnedFolders([existing, missing]);
    testState.resolvedFolderIds = [existing];
    updatePreferencesApiMock.mockResolvedValue({
      [USER_PREFERENCE_KEYS.dashboardPinnedFolderIds]: serializedExisting,
    });

    renderHook(() => usePinnedFolders());

    await waitFor(() => {
      expect(updatePreferencesApiMock).toHaveBeenCalledWith({
        [USER_PREFERENCE_KEYS.dashboardPinnedFolderIds]: serializedExisting,
      });
    });
  });

  it("does not remove ids when resolving folders failed", () => {
    hydratePinnedFolders([folderId(1)]);
    testState.isError = true;
    testState.isSuccess = false;

    renderHook(() => usePinnedFolders());

    expect(updatePreferencesApiMock).not.toHaveBeenCalled();
  });

  it("does not loop when a cleanup save rolls back", async () => {
    const existing = folderId(1);
    const missing = folderId(2);
    hydratePinnedFolders([existing, missing]);
    testState.resolvedFolderIds = [existing];
    updatePreferencesApiMock.mockRejectedValue(new Error("offline"));

    renderHook(() => usePinnedFolders());

    await waitFor(() => {
      expect(useUserPreferencesStore.getState().syncing).toBe(false);
      expect(updatePreferencesApiMock).toHaveBeenCalledOnce();
    });
  });
});
