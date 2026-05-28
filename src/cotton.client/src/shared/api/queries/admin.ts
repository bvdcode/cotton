import {
  useMutation,
  useQuery,
  useQueryClient,
  type QueryClient,
} from "@tanstack/react-query";
import {
  adminApi,
  type AdminCreateUserRequestDto,
  type AdminUpdateUserRequestDto,
  type AdminUserDto,
  type GcChunkTimelineDto,
  type GcTimelineBucketKind,
  type KeyringRecoveryKitDto,
  type KeyringRotateUnlockRequestDto,
  type LatestDatabaseBackupDto,
  type SecurityDiagnosticsDto,
} from "../adminApi";
import { queryKeys } from "./queryKeys";

export interface AdminUsersQueryOptions {
  withStorage: boolean;
  enabled?: boolean;
}

export interface GcTimelineQueryRequest {
  bucket: GcTimelineBucketKind;
  fromUtc?: string;
  toUtc?: string;
}

export const mergeUsersWithStorageUsage = (
  users: AdminUserDto[],
  usersWithStorage: AdminUserDto[] | undefined,
): AdminUserDto[] => {
  if (!usersWithStorage) {
    return users;
  }

  const storageByUserId = new Map(
    usersWithStorage.map((user) => [user.id, user.storageUsedBytes]),
  );

  return users.map((user) => {
    const storageUsedBytes = storageByUserId.get(user.id);
    return storageUsedBytes === undefined
      ? user
      : { ...user, storageUsedBytes };
  });
};

export const invalidateAdminUsers = async (
  queryClient: QueryClient,
): Promise<void> => {
  await queryClient.invalidateQueries({
    queryKey: queryKeys.admin.users.all(),
  });
};

export const invalidateGcTimeline = async (
  queryClient: QueryClient,
): Promise<void> => {
  await queryClient.invalidateQueries({
    queryKey: queryKeys.admin.gcTimeline.all(),
  });
};

export const clearAdminCaches = (queryClient: QueryClient): void => {
  queryClient.removeQueries({ queryKey: queryKeys.admin.all() });
};

export const useAdminUsersQuery = (options: AdminUsersQueryOptions) =>
  useQuery<AdminUserDto[]>({
    queryKey: queryKeys.admin.users.list({
      withStorage: options.withStorage,
    }),
    queryFn: ({ signal }) =>
      adminApi.getUsers({
        calculateStorageUsage: options.withStorage || undefined,
        signal,
      }),
    enabled: options.enabled ?? true,
  });

export const useGcChunksTimelineQuery = (request: GcTimelineQueryRequest) =>
  useQuery<GcChunkTimelineDto>({
    queryKey: queryKeys.admin.gcTimeline.detail(request),
    queryFn: ({ signal }) =>
      adminApi.getGcChunksTimeline({
        ...request,
        signal,
      }),
  });

export const useLatestDatabaseBackupQuery = () =>
  useQuery<LatestDatabaseBackupDto | null>({
    queryKey: queryKeys.admin.latestDbBackup(),
    queryFn: ({ signal }) => adminApi.getLatestDatabaseBackup(signal),
  });

export const useSecurityDiagnosticsQuery = () =>
  useQuery<SecurityDiagnosticsDto>({
    queryKey: queryKeys.admin.securityDiagnostics(),
    queryFn: ({ signal }) => adminApi.getSecurityDiagnostics(signal),
  });

export const useRotateKeyringUnlockMutation = () => {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: (request: KeyringRotateUnlockRequestDto) =>
      adminApi.rotateKeyringUnlock(request),
    onSuccess: () =>
      queryClient.invalidateQueries({
        queryKey: queryKeys.admin.securityDiagnostics(),
      }),
  });
};

export const useExportKeyringRecoveryKitMutation = () =>
  useMutation({
    mutationFn: () => adminApi.exportKeyringRecoveryKit(),
  });

export const useImportKeyringRecoveryKitMutation = () => {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: (kit: KeyringRecoveryKitDto) =>
      adminApi.importKeyringRecoveryKit(kit),
    onSuccess: () =>
      queryClient.invalidateQueries({
        queryKey: queryKeys.admin.securityDiagnostics(),
      }),
  });
};

export const useCreateAdminUserMutation = () => {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: (request: AdminCreateUserRequestDto) =>
      adminApi.createUser(request),
    onSuccess: () => invalidateAdminUsers(queryClient),
  });
};

export const useUpdateAdminUserMutation = () => {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: (vars: {
      userId: string;
      request: AdminUpdateUserRequestDto;
    }) => adminApi.updateUser(vars.userId, vars.request),
    onSuccess: () => invalidateAdminUsers(queryClient),
  });
};

export const useTriggerGarbageCollectorMutation = () => {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: () => adminApi.triggerGarbageCollector(),
    onSuccess: () => invalidateGcTimeline(queryClient),
  });
};

export const useTriggerDatabaseBackupMutation = () => {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: () => adminApi.triggerDatabaseBackup(),
    onSuccess: () =>
      queryClient.invalidateQueries({
        queryKey: queryKeys.admin.latestDbBackup(),
      }),
  });
};
