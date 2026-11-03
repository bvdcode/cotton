import { useQuery, type UseQueryResult } from "@tanstack/react-query";
import { useMemo } from "react";
import {
  nodesApi,
  type NodeContentDto,
  type NodeResponse,
} from "../../../shared/api/nodesApi";
import { queryKeys } from "../../../shared/api/queries/queryKeys";
import type { PaginationProps } from "../../../shared/types/FileListViewTypes";
import { useFolderEncryptedNames } from "./useFolderEncryptedNames";
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
  const encryptedNames = useFolderEncryptedNames({
    nodeId,
    enabled: enabled && !folderPolicyEnabled && overview.data !== undefined,
    firstPage: overview.data
      ? { response: overview.data, pageSize }
      : undefined,
  });
  const namesEnabled = !folderPolicyEnabled && encryptedNames === false;
  const controls = useFolderListControls(nodeId, namesEnabled);
  const { page, listing } = controls;
  const batch = Math.floor((page * LIST_PAGE_SIZE) / FOLDER_PAGE_SIZE);
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

  const listPage = useQuery<NodeResponse>({
    placeholderData: (previous, query) =>
      query?.queryKey[1] === queryNodeId && query.queryKey[2] === queryUserId
        ? previous
        : undefined,
    queryKey: queryKeys.nodeChildren.page(
      queryNodeId,
      queryUserId,
      batch,
      listing,
    ),
    queryFn: ({ signal }) =>
      nodesApi.getChildren(queryNodeId, {
        page: batch + 1,
        pageSize: FOLDER_PAGE_SIZE,
        signal,
        ...(defaultListing ? {} : { listing }),
      }),
    enabled: enabled && !needsCalculatedSizes && (batch > 0 || !defaultListing),
  });

  const activePage = needsCalculatedSizes
    ? complete
    : batch === 0 && defaultListing
      ? overview
      : listPage;
  const content = useMemo(() => {
    const loaded = activePage.data?.content;
    if (!loaded || needsCalculatedSizes) {
      return loaded;
    }
    return sliceListPage(loaded, (page * LIST_PAGE_SIZE) % FOLDER_PAGE_SIZE);
  }, [activePage.data?.content, needsCalculatedSizes, page]);
  return {
    content,
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

function sliceListPage(
  content: NodeContentDto,
  offset: number,
): NodeContentDto {
  const end = offset + LIST_PAGE_SIZE;
  return {
    ...content,
    nodes: content.nodes.slice(offset, end),
    files: content.files.slice(
      Math.max(0, offset - content.nodes.length),
      Math.max(0, end - content.nodes.length),
    ),
  };
}
