import { QueryClient } from "@tanstack/react-query";
import { describe, expect, it } from "vitest";
import { UserRole } from "../../../features/auth/types";
import type { AdminUserDto } from "../adminApi";
import { clearAdminCaches, mergeUsersWithStorageUsage } from "./admin";
import { queryKeys } from "./queryKeys";

const createQueryClient = () =>
  new QueryClient({
    defaultOptions: {
      queries: { retry: false },
      mutations: { retry: false },
    },
  });

const createAdminUser = (
  id: string,
  storageUsedBytes: number,
): AdminUserDto => ({
  id,
  createdAt: "2026-05-17T00:00:00Z",
  updatedAt: "2026-05-17T00:00:00Z",
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
  storageUsedBytes,
});

describe("admin query cache helpers", () => {
  it("merges calculated storage usage into the fast user list", () => {
    const first = createAdminUser("first", 0);
    const second = createAdminUser("second", 0);

    const merged = mergeUsersWithStorageUsage([first, second], [
      createAdminUser("first", 1024),
      createAdminUser("unknown", 2048),
    ]);

    expect(merged.map((user) => [user.id, user.storageUsedBytes])).toEqual([
      ["first", 1024],
      ["second", 0],
    ]);
    expect(merged[1]).toBe(second);
  });

  it("leaves users unchanged until storage usage is available", () => {
    const users = [createAdminUser("first", 0)];

    expect(mergeUsersWithStorageUsage(users, undefined)).toBe(users);
  });

  it("clears all admin query caches", () => {
    const queryClient = createQueryClient();

    queryClient.setQueryData(
      queryKeys.admin.users.list({ withStorage: false }),
      [createAdminUser("first", 0)],
    );
    queryClient.setQueryData(
      queryKeys.admin.gcTimeline.detail({ bucket: "day" }),
      { totalChunks: 1 },
    );
    queryClient.setQueryData(queryKeys.admin.latestDbBackup(), {
      backupId: "backup-id",
    });

    clearAdminCaches(queryClient);

    expect(
      queryClient.getQueryData(
        queryKeys.admin.users.list({ withStorage: false }),
      ),
    ).toBeUndefined();
    expect(
      queryClient.getQueryData(
        queryKeys.admin.gcTimeline.detail({ bucket: "day" }),
      ),
    ).toBeUndefined();
    expect(
      queryClient.getQueryData(queryKeys.admin.latestDbBackup()),
    ).toBeUndefined();
  });
});
