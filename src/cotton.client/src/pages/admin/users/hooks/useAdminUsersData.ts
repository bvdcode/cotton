import { useCallback, useMemo } from "react";
import { useQueryClient } from "@tanstack/react-query";
import { useTranslation } from "react-i18next";
import { getApiErrorMessage } from "../../../../shared/api/httpClient";
import type { AdminUserDto } from "../../../../shared/api/adminApi";
import {
  invalidateAdminUsers,
  mergeUsersWithStorageUsage,
  useAdminUsersQuery,
} from "../../../../shared/api/queries/admin";

export type LoadState =
  | { kind: "idle" }
  | { kind: "loading" }
  | { kind: "error"; message: string };

export interface AdminUsersData {
  users: AdminUserDto[];
  loadState: LoadState;
  storageUsageLoading: boolean;
  refresh: () => Promise<void>;
}

export const useAdminUsersData = (): AdminUsersData => {
  const { t } = useTranslation("admin");
  const queryClient = useQueryClient();

  const fastQuery = useAdminUsersQuery({ withStorage: false });
  const storageQuery = useAdminUsersQuery({
    withStorage: true,
    enabled: fastQuery.isSuccess && (fastQuery.data?.length ?? 0) > 0,
  });

  const users = useMemo(
    () => mergeUsersWithStorageUsage(fastQuery.data ?? [], storageQuery.data),
    [fastQuery.data, storageQuery.data],
  );

  const loadState = useMemo<LoadState>(() => {
    if (fastQuery.isPending || fastQuery.isFetching) {
      return { kind: "loading" };
    }

    if (fastQuery.isError) {
      return {
        kind: "error",
        message:
          getApiErrorMessage(fastQuery.error) ?? t("users.errors.loadFailed"),
      };
    }

    return { kind: "idle" };
  }, [
    fastQuery.error,
    fastQuery.isError,
    fastQuery.isFetching,
    fastQuery.isPending,
    t,
  ]);

  const storageUsageLoading = storageQuery.fetchStatus === "fetching";

  const refresh = useCallback(
    () => invalidateAdminUsers(queryClient),
    [queryClient],
  );

  return { users, loadState, storageUsageLoading, refresh };
};


