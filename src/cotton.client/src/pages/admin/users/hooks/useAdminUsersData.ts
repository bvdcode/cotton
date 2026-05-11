import { useCallback, useEffect, useRef, useState } from "react";
import { useTranslation } from "react-i18next";
import {
  getApiErrorMessage,
  isAxiosError,
} from "../../../../shared/api/httpClient";
import { adminApi, type AdminUserDto } from "../../../../shared/api/adminApi";

export type LoadState =
  | { kind: "idle" }
  | { kind: "loading" }
  | { kind: "error"; message: string };

const isRequestCancelled = (error: unknown, signal: AbortSignal): boolean =>
  signal.aborted || (isAxiosError(error) && error.code === "ERR_CANCELED");

export interface AdminUsersData {
  users: AdminUserDto[];
  loadState: LoadState;
  storageUsageLoading: boolean;
  refresh: () => Promise<void>;
}

export const useAdminUsersData = (): AdminUsersData => {
  const { t } = useTranslation("admin");
  const [users, setUsers] = useState<AdminUserDto[]>([]);
  const [loadState, setLoadState] = useState<LoadState>({ kind: "loading" });
  const [storageUsageLoading, setStorageUsageLoading] = useState(false);
  const usersRequestControllerRef = useRef<AbortController | null>(null);

  const loadStorageUsage = useCallback(async (signal: AbortSignal) => {
    try {
      const result = await adminApi.getUsers({
        calculateStorageUsage: true,
        signal,
      });

      if (signal.aborted) {
        return;
      }

      const storageUsageByUserId = new Map(
        result.map((user) => [user.id, user.storageUsedBytes]),
      );

      setUsers((current) =>
        current.map((user) => {
          const storageUsedBytes = storageUsageByUserId.get(user.id);
          return storageUsedBytes === undefined
            ? user
            : { ...user, storageUsedBytes };
        }),
      );
    } catch (error) {
      if (isRequestCancelled(error, signal)) {
        return;
      }

      // The fast user list is still useful; leave storage usage at its
      // server-provided fallback if the secondary calculation fails.
    } finally {
      if (!signal.aborted) {
        setStorageUsageLoading(false);
      }
    }
  }, []);

  const fetchUsers = useCallback(async () => {
    usersRequestControllerRef.current?.abort();

    const controller = new AbortController();
    usersRequestControllerRef.current = controller;
    const { signal } = controller;
    let storageUsageStarted = false;

    setLoadState({ kind: "loading" });
    setStorageUsageLoading(false);

    try {
      const result = await adminApi.getUsers({ signal });

      if (signal.aborted) {
        return;
      }

      setUsers(result);
      setLoadState({ kind: "idle" });

      if (result.length === 0) {
        if (usersRequestControllerRef.current === controller) {
          usersRequestControllerRef.current = null;
        }
        return;
      }

      setStorageUsageLoading(true);
      storageUsageStarted = true;
      void loadStorageUsage(signal).finally(() => {
        if (usersRequestControllerRef.current === controller) {
          usersRequestControllerRef.current = null;
        }
      });
    } catch (error) {
      if (isRequestCancelled(error, signal)) {
        return;
      }

      const message = getApiErrorMessage(error);
      if (message) {
        setLoadState({ kind: "error", message });
        return;
      }

      setLoadState({ kind: "error", message: t("users.errors.loadFailed") });
    } finally {
      if (
        !storageUsageStarted &&
        usersRequestControllerRef.current === controller
      ) {
        usersRequestControllerRef.current = null;
      }
    }
  }, [loadStorageUsage, t]);

  useEffect(() => {
    void fetchUsers();

    return () => {
      usersRequestControllerRef.current?.abort();
      usersRequestControllerRef.current = null;
    };
  }, [fetchUsers]);

  return { users, loadState, storageUsageLoading, refresh: fetchUsers };
};


