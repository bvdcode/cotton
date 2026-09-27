import { reportClientError } from "@shared/utils/clientDiagnostics";
import { nodesApi, type NodeContentDto } from "../api/nodesApi";
import { useNodesStore } from "./nodesStore";

const CHILDREN_FETCH_PAGE_SIZE = 100_000;

export async function fetchAllNodeChildren(
  nodeId: string,
): Promise<NodeContentDto> {
  const firstPage = await nodesApi.getChildren(nodeId, {
    page: 1,
    pageSize: CHILDREN_FETCH_PAGE_SIZE,
  });

  let merged: NodeContentDto = {
    ...firstPage.content,
    nodes: [...firstPage.content.nodes],
    files: [...firstPage.content.files],
  };

  const totalCount = firstPage.totalCount;
  let page = 2;

  while (merged.nodes.length + merged.files.length < totalCount) {
    const response = await nodesApi.getChildren(nodeId, {
      page,
      pageSize: CHILDREN_FETCH_PAGE_SIZE,
    });

    if (
      response.content.nodes.length === 0 &&
      response.content.files.length === 0
    ) {
      break;
    }

    merged = {
      ...merged,
      nodes: [...merged.nodes, ...response.content.nodes],
      files: [...merged.files, ...response.content.files],
    };

    page += 1;
  }

  return merged;
}

export const refreshNodeContent = async (nodeId: string): Promise<void> => {
  try {
    const content = await fetchAllNodeChildren(nodeId);
    useNodesStore.setState((prev) => ({
      contentByNodeId: {
        ...prev.contentByNodeId,
        [nodeId]: content,
      },
      lastUpdatedByNodeId: {
        ...prev.lastUpdatedByNodeId,
        [nodeId]: Date.now(),
      },
    }));
  } catch (error) {
    reportClientError("Failed to refresh node content", error);
  }
};
