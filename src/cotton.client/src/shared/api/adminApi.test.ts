import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";

vi.mock("react-toastify", () => ({
  toast: { error: vi.fn() },
}));

vi.mock("../i18n/translateError", () => ({
  translateError: (namespace: string, key: string) => `${namespace}:${key}`,
}));

vi.mock("../store/authStore", () => ({
  getRefreshEnabled: () => true,
  useAuthStore: {
    getState: () => ({
      logoutLocal: vi.fn(),
    }),
  },
}));

const { httpClient } = await import("./httpClient");
const { adminApi } = await import("./adminApi");

const makeAdminUser = () => ({
  id: "user-1",
  createdAt: "2026-05-17T00:00:00Z",
  updatedAt: "2026-05-17T00:00:01Z",
  username: "alice",
  email: "alice@example.com",
  role: 2,
  firstName: "Alice",
  lastName: null,
  birthDate: null,
  isTotpEnabled: false,
  totpEnabledAt: null,
  totpFailedAttempts: 0,
  lastActivityAt: null,
  activeSessionCount: 1,
  storageUsedBytes: 1024,
});

const axiosLikeError = (status: number) => ({
  isAxiosError: true,
  response: { status },
  config: { url: "server/database-backup/latest" },
  message: String(status),
  name: "AxiosError",
  toJSON: () => ({}),
});

beforeEach(() => {
  vi.spyOn(console, "error").mockImplementation(() => undefined);
});

afterEach(() => {
  vi.restoreAllMocks();
});

describe("adminApi.getUsers", () => {
  it("omits calculateStorageUsage unless explicitly enabled", async () => {
    const get = vi.spyOn(httpClient, "get").mockResolvedValue({
      data: [makeAdminUser()],
    });

    const users = await adminApi.getUsers();

    expect(users).toHaveLength(1);
    expect(get).toHaveBeenCalledWith("users", {
      params: { calculateStorageUsage: undefined },
      signal: undefined,
    });

    await adminApi.getUsers({ calculateStorageUsage: false });
    expect(get).toHaveBeenLastCalledWith("users", {
      params: { calculateStorageUsage: undefined },
      signal: undefined,
    });
  });

  it("includes storage usage and forwards abort signal when requested", async () => {
    const get = vi.spyOn(httpClient, "get").mockResolvedValue({
      data: [],
    });
    const controller = new AbortController();

    await adminApi.getUsers({
      calculateStorageUsage: true,
      signal: controller.signal,
    });

    expect(get).toHaveBeenCalledWith("users", {
      params: { calculateStorageUsage: true },
      signal: controller.signal,
    });
  });
});

describe("adminApi users", () => {
  it("creates users through the users endpoint", async () => {
    const post = vi.spyOn(httpClient, "post").mockResolvedValue({
      data: undefined,
    });
    const request = {
      username: "alice",
      email: "alice@example.com",
      password: "secret",
      role: 2 as const,
      firstName: "Alice",
      lastName: null,
      birthDate: null,
    };

    await adminApi.createUser(request);

    expect(post).toHaveBeenCalledWith("users", request);
  });

  it("updates users and returns the updated DTO", async () => {
    const updatedUser = {
      ...makeAdminUser(),
      username: "alice2",
    };
    const put = vi.spyOn(httpClient, "put").mockResolvedValue({
      data: updatedUser,
    });
    const request = {
      username: "alice2",
      email: "alice2@example.com",
      role: 1 as const,
      firstName: null,
      lastName: null,
      birthDate: null,
    };

    await expect(adminApi.updateUser("user-1", request)).resolves.toBe(
      updatedUser,
    );
    expect(put).toHaveBeenCalledWith("users/user-1", request);
  });
});

describe("adminApi database backups", () => {
  it("returns the latest backup descriptor", async () => {
    const backup = {
      backupId: "backup-1",
      createdAtUtc: "2026-05-17T00:00:00Z",
      pointerUpdatedAtUtc: "2026-05-17T00:01:00Z",
      dumpSizeBytes: 123,
      chunkCount: 2,
      dumpContentHash: "hash",
      sourceDatabase: "cotton",
      sourceHost: "localhost",
      sourcePort: 5432,
    };
    const get = vi.spyOn(httpClient, "get").mockResolvedValue({
      data: backup,
    });
    const controller = new AbortController();

    await expect(
      adminApi.getLatestDatabaseBackup(controller.signal),
    ).resolves.toBe(backup);
    expect(get).toHaveBeenCalledWith("server/database-backup/latest", {
      signal: controller.signal,
    });
  });

  it("returns null when no backup exists yet", async () => {
    vi.spyOn(httpClient, "get").mockRejectedValue(axiosLikeError(404));

    await expect(adminApi.getLatestDatabaseBackup()).resolves.toBeNull();
  });

  it("rethrows non-404 backup errors", async () => {
    vi.spyOn(httpClient, "get").mockRejectedValue(axiosLikeError(500));

    await expect(adminApi.getLatestDatabaseBackup()).rejects.toMatchObject({
      response: { status: 500 },
    });
  });

  it("triggers a backup through PATCH", async () => {
    const patch = vi.spyOn(httpClient, "patch").mockResolvedValue({
      data: undefined,
    });

    await adminApi.triggerDatabaseBackup();

    expect(patch).toHaveBeenCalledWith("server/database-backup/trigger");
  });
});

describe("adminApi garbage collection", () => {
  it("triggers garbage collection through PATCH", async () => {
    const patch = vi.spyOn(httpClient, "patch").mockResolvedValue({
      data: undefined,
    });

    await adminApi.triggerGarbageCollector();

    expect(patch).toHaveBeenCalledWith("server/gc/trigger");
  });

  it("threads filter params and signal into the GC timeline call", async () => {
    const timeline = {
      bucket: "hour",
      from: "2026-05-16T00:00:00Z",
      to: "2026-05-17T00:00:00Z",
      generatedAt: "2026-05-17T00:00:01Z",
      totalChunks: 1,
      totalSizeBytes: 100,
      buckets: [],
      storage: {
        storageType: "Local",
        totalUniqueChunkCount: 1,
        totalUniqueChunkPlainSizeBytes: 100,
        totalUniqueChunkStoredSizeBytes: 100,
        referencedUniqueChunkCount: 1,
        referencedUniqueChunkPlainSizeBytes: 100,
        referencedUniqueChunkStoredSizeBytes: 100,
        referencedLogicalChunkCount: 1,
        referencedLogicalPlainSizeBytes: 100,
        deduplicatedUniqueChunkCount: 0,
        dedupSavedBytes: 0,
        compressionSavedBytes: 0,
        pendingGcChunkCount: 0,
        pendingGcStoredSizeBytes: 0,
        overdueGcChunkCount: 0,
        overdueGcStoredSizeBytes: 0,
      },
    };
    const get = vi.spyOn(httpClient, "get").mockResolvedValue({
      data: timeline,
    });
    const controller = new AbortController();

    await expect(
      adminApi.getGcChunksTimeline({
        bucket: "hour",
        fromUtc: "2026-05-16T00:00:00Z",
        toUtc: "2026-05-17T00:00:00Z",
        signal: controller.signal,
      }),
    ).resolves.toBe(timeline);

    expect(get).toHaveBeenCalledWith("server/gc/chunks/timeline", {
      params: {
        bucket: "hour",
        fromUtc: "2026-05-16T00:00:00Z",
        toUtc: "2026-05-17T00:00:00Z",
      },
      signal: controller.signal,
    });
  });
});
