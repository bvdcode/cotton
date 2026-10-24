import { useQuery, type UseQueryResult } from "@tanstack/react-query";
import { nodesApi, type NodeResponse } from "../../../shared/api/nodesApi";
import { queryKeys } from "../../../shared/api/queries/queryKeys";
import type { PaginationProps } from "../../../shared/types/FileListViewTypes";
import { useFolderEncryptionPresence } from "./useFolderEncryptionPresence";
import { useFolderListControls } from "./useFolderListControls";
import { fetchAllRawNodeChildren } from "@shared/api/nodeChildren";
import { FOLDER_PAGE_SIZE } from "@shared/api/queries/folderPageSize";

export const LIST_PAGE_SIZE = 100;

interface FolderListPageOptions {
  nodeId: string | null;
  userId: string | null;
  enabled: boolean;
  folderPolicyEnabled: boolean;
  overview: UseQueryResult<NodeResponse, Error>;
  pageSize: number;
}

export function useFolderListPage({
  nodeId,
  userId,
  enabled,
  folderPolicyEnabled,
  overview,
  pageSize,
}: FolderListPageOptions) {
  const queryNodeId = nodeId ?? "";
  const queryUserId = userId ?? "";
  const encryptedNames = useFolderEncryptionPresence({
    nodeId,
    match: "encrypted-names",
    enabled: enabled && !folderPolicyEnabled && overview.data !== undefined,
    firstPage: overview.data
      ? { response: overview.data, pageSize }
      : undefined,
  });
  const namesEnabled = !folderPolicyEnabled && encryptedNames === false;
  const controls = useFolderListControls(nodeId, namesEnabled);
  const { page, listing } = controls;
  const defaultListing = Object.keys(listing).length === 0;
  const needsCalculatedSizes =
    listing.sortBy === "SizeBytes" || listing.filterBy === "SizeBytes";
  const complete = useQuery({
    queryKey: queryKeys.nodeChildren.complete(queryNodeId, queryUserId),
    queryFn: ({ signal }) =>
      fetchAllRawNodeChildren(queryNodeId, {
        signal,
        pageSize: FOLDER_PAGE_SIZE,
      }),
    enabled: enabled && needsCalculatedSizes,
  });

  const listPage = useQuery({
    placeholderData: (previous, query) =>
      query?.queryKey[1] === queryNodeId && query.queryKey[2] === queryUserId
        ? previous
        : undefined,
    queryKey: queryKeys.nodeChildren.page(
      queryNodeId,
      queryUserId,
      page,
      listing,
    ),
    queryFn: () =>
      nodesApi.getChildren(queryNodeId, {
        page: page + 1,
        pageSize: LIST_PAGE_SIZE,
        ...(defaultListing ? {} : { listing }),
      }),
    enabled: enabled && !needsCalculatedSizes && (page > 0 || !defaultListing),
  });

  const activePage = needsCalculatedSizes
    ? complete
    : page === 0 && defaultListing
      ? overview
      : listPage;
  return {
    content: activePage.data?.content,
    isError: activePage.isError,
    isPending: activePage.isPending,
    pagination: {
      ...getPagination(controls, activePage, overview),
      mode: needsCalculatedSizes ? ("client" as const) : ("server" as const),
    },
  };
}

function getPagination(
  controls: ReturnType<typeof useFolderListControls>,
  activePage: UseQueryResult<NodeResponse, Error>,
  overview: UseQueryResult<NodeResponse, Error>,
): PaginationProps {
  return {
    query: controls.query,
    model: { page: controls.page, pageSize: LIST_PAGE_SIZE },
    totalCount: activePage.data?.totalCount ?? overview.data?.totalCount ?? 0,
    loading: activePage.isFetching,
    onPaginationModelChange: (model) => controls.setPage(model.page),
  };
}
