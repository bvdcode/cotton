import {
  useMutation,
  useQuery,
  useQueryClient,
  type QueryClient,
} from "@tanstack/react-query";
import { filesApi, type FileVersionDto } from "../filesApi";
import type { NodeFileManifestDto } from "../nodesApi";
import { queryKeys } from "./queryKeys";

interface FileVersionMutationRequest {
  fileId: string;
  versionId: string;
}

const requireFileId = (fileId: string | null | undefined): string => {
  if (!fileId) {
    throw new Error("File versions query requires a fileId");
  }

  return fileId;
};

export const invalidateFileVersions = (
  queryClient: QueryClient,
  fileId: string,
): Promise<void> =>
  queryClient.invalidateQueries({
    queryKey: queryKeys.fileVersions.list(fileId),
  });

export const useFileVersionsQuery = (
  fileId: string | null | undefined,
  enabled: boolean,
) =>
  useQuery<FileVersionDto[]>({
    queryKey: queryKeys.fileVersions.list(fileId ?? ""),
    queryFn: () => filesApi.listVersions(requireFileId(fileId)),
    enabled: enabled && !!fileId,
  });

export const useRestoreFileVersionMutation = () => {
  const queryClient = useQueryClient();

  return useMutation<NodeFileManifestDto, Error, FileVersionMutationRequest>({
    mutationFn: ({ fileId, versionId }) =>
      filesApi.restoreVersion(fileId, versionId),
    onSuccess: async (_restored, request) =>
      invalidateFileVersions(queryClient, request.fileId),
  });
};

export const useDeleteFileVersionMutation = () => {
  const queryClient = useQueryClient();

  return useMutation<void, Error, FileVersionMutationRequest>({
    mutationFn: ({ fileId, versionId }) =>
      filesApi.deleteVersion(fileId, versionId),
    onSuccess: async (_empty, request) =>
      invalidateFileVersions(queryClient, request.fileId),
  });
};
