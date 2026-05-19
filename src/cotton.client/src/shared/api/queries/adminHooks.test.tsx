import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import type { QueryClientConfig } from "@tanstack/react-query";
import { renderHook, waitFor } from "@testing-library/react";
import type { ReactNode } from "react";
import { afterEach, describe, expect, it, vi } from "vitest";
import { UserRole } from "../../../features/auth/types";
import type { AdminUserDto } from "../adminApi";

vi.mock("@shared/ui/notifications", () => ({
  toast: { error: vi.fn() },
}));

vi.mock("../../i18n/translateError", () => ({
  translateError: (namespace: string, key: string) => `${namespace}:${key}`,
}));

vi.mock("../../store/authStore", () => ({
  getRefreshEnabled: () => true,
  useAuthStore: {
    getState: () => ({
      logoutLocal: vi.fn(),
    }),
  },
}));

const { adminApi } = await import("../adminApi");
const { queryKeys } = await import("./queryKeys");
const {
  useAdminUsersQuery,
  useCreateAdminUserMutation,
  useGcChunksTimelineQuery,
  useLatestDatabaseBackupQuery,
  useTriggerDatabaseBackupMutation,
  useTriggerGarbageCollectorMutation,
  useUpdateAdminUserMutation,
} = await import("./admin");

const queryClientConfig: QueryClientConfig = {
  defaultOptions: {
    queries: { retry: false },
    mutations: { retry: false },
  },
};

const createQueryClient = () => new QueryClient(queryClientConfig);

const createWrapper =
  (queryClient: QueryClient) =>
  ({ children }: { children: ReactNode }) => (
    <QueryClientProvider client={queryClient}>{children}</QueryClientProvider>
  );

const makeAdminUser = (id: string): AdminUserDto => ({
  id,
  createdAt: "2026-05-17T00:00:00Z",
  updatedAt: "2026-05-17T00:00:01Z",
  username: `user-${id}`,
  email: null,
  role: UserRole.User,
  firstName: null,
  lastName: null,
  birthDate: null,
  isTotpEnabled: false,
  totpEnabledAt: null,
  totpFailedAttempts: 0,
  lastActivityAt: null,
  activeSessionCount: 0,
  storageUsedBytes: 0,
});

afterEach(() => {
  vi.restoreAllMocks();
});

describe("useAdminUsersQuery", () => {
  it("calls adminApi.getUsers and caches under the requested variant key", async () => {
    const users = [makeAdminUser("1")];
    const getUsers = vi.spyOn(adminApi, "getUsers").mockResolvedValue(users);
    const queryClient = createQueryClient();

    const { result } = renderHook(
      () => useAdminUsersQuery({ withStorage: true }),
      { wrapper: createWrapper(queryClient) },
    );

    await waitFor(() => expect(result.current.isSuccess).toBe(true));

    expect(getUsers).toHaveBeenCalledWith(
      expect.objectContaining({ calculateStorageUsage: true }),
    );
    expect(
      queryClient.getQueryData(
        queryKeys.admin.users.list({ withStorage: true }),
      ),
    ).toEqual(users);
  });

  it("respects the enabled flag", async () => {
    const getUsers = vi.spyOn(adminApi, "getUsers").mockResolvedValue([]);
    const queryClient = createQueryClient();

    renderHook(
      () => useAdminUsersQuery({ withStorage: false, enabled: false }),
      { wrapper: createWrapper(queryClient) },
    );

    await new Promise((resolve) => setTimeout(resolve, 10));
    expect(getUsers).not.toHaveBeenCalled();
  });
});

describe("admin query hooks", () => {
  it("returns the latest database backup value, including null", async () => {
    vi.spyOn(adminApi, "getLatestDatabaseBackup").mockResolvedValue(null);
    const queryClient = createQueryClient();

    const { result } = renderHook(() => useLatestDatabaseBackupQuery(), {
      wrapper: createWrapper(queryClient),
    });

    await waitFor(() => expect(result.current.isSuccess).toBe(true));
    expect(result.current.data).toBeNull();
  });

  it("passes the GC timeline range and query signal to the API", async () => {
    const getTimeline = vi
      .spyOn(adminApi, "getGcChunksTimeline")
      .mockResolvedValue({
        bucket: "day",
        from: "2026-05-01T00:00:00Z",
        to: "2026-05-02T00:00:00Z",
        generatedAt: "2026-05-02T00:00:01Z",
        totalChunks: 0,
        totalSizeBytes: 0,
        buckets: [],
        storage: {} as never,
      });
    const queryClient = createQueryClient();

    const { result } = renderHook(
      () =>
        useGcChunksTimelineQuery({
          bucket: "day",
          fromUtc: "2026-05-01T00:00:00Z",
          toUtc: "2026-05-02T00:00:00Z",
        }),
      { wrapper: createWrapper(queryClient) },
    );

    await waitFor(() => expect(result.current.isSuccess).toBe(true));
    expect(getTimeline).toHaveBeenCalledWith(
      expect.objectContaining({
        bucket: "day",
        fromUtc: "2026-05-01T00:00:00Z",
        toUtc: "2026-05-02T00:00:00Z",
        signal: expect.any(AbortSignal),
      }),
    );
  });
});

describe("admin mutations", () => {
  it("invalidates admin user caches after creating a user", async () => {
    vi.spyOn(adminApi, "createUser").mockResolvedValue();
    const queryClient = createQueryClient();
    queryClient.setQueryData(
      queryKeys.admin.users.list({ withStorage: false }),
      [],
    );
    queryClient.setQueryData(
      queryKeys.admin.users.list({ withStorage: true }),
      [],
    );

    const { result } = renderHook(() => useCreateAdminUserMutation(), {
      wrapper: createWrapper(queryClient),
    });

    await result.current.mutateAsync({
      username: "new-user",
      email: null,
      password: "secret",
      role: UserRole.User,
      firstName: null,
      lastName: null,
      birthDate: null,
    });

    expect(
      queryClient
        .getQueryCache()
        .findAll({ queryKey: queryKeys.admin.users.all() })
        .every((query) => query.state.isInvalidated),
    ).toBe(true);
  });

  it("invalidates admin user caches after updating a user", async () => {
    vi.spyOn(adminApi, "updateUser").mockResolvedValue(makeAdminUser("1"));
    const queryClient = createQueryClient();
    queryClient.setQueryData(
      queryKeys.admin.users.list({ withStorage: false }),
      [makeAdminUser("1")],
    );

    const { result } = renderHook(() => useUpdateAdminUserMutation(), {
      wrapper: createWrapper(queryClient),
    });

    await result.current.mutateAsync({
      userId: "1",
      request: {
        username: "updated",
        email: null,
        role: UserRole.Admin,
        firstName: null,
        lastName: null,
        birthDate: null,
      },
    });

    expect(
      queryClient
        .getQueryCache()
        .findAll({ queryKey: queryKeys.admin.users.all() })
        .every((query) => query.state.isInvalidated),
    ).toBe(true);
  });

  it("invalidates every GC timeline range after triggering GC", async () => {
    vi.spyOn(adminApi, "triggerGarbageCollector").mockResolvedValue();
    const queryClient = createQueryClient();
    queryClient.setQueryData(
      queryKeys.admin.gcTimeline.detail({ bucket: "hour" }),
      "hour",
    );
    queryClient.setQueryData(
      queryKeys.admin.gcTimeline.detail({ bucket: "day" }),
      "day",
    );

    const { result } = renderHook(
      () => useTriggerGarbageCollectorMutation(),
      { wrapper: createWrapper(queryClient) },
    );

    await result.current.mutateAsync();

    expect(
      queryClient
        .getQueryCache()
        .findAll({ queryKey: queryKeys.admin.gcTimeline.all() })
        .every((query) => query.state.isInvalidated),
    ).toBe(true);
  });

  it("invalidates latest database backup after triggering a backup", async () => {
    vi.spyOn(adminApi, "triggerDatabaseBackup").mockResolvedValue();
    const queryClient = createQueryClient();
    queryClient.setQueryData(queryKeys.admin.latestDbBackup(), {
      backupId: "backup-1",
    });

    const { result } = renderHook(() => useTriggerDatabaseBackupMutation(), {
      wrapper: createWrapper(queryClient),
    });

    await result.current.mutateAsync();

    expect(
      queryClient
        .getQueryCache()
        .find({ queryKey: queryKeys.admin.latestDbBackup() })?.state
        .isInvalidated,
    ).toBe(true);
  });
});
