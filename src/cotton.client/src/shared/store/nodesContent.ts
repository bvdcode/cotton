import { reportClientError } from "@shared/utils/clientDiagnostics";
import { fetchAllNodeChildren } from "../api/nodeChildren";
import { useNodesStore } from "./nodesStore";

export const refreshNodeContent = async (nodeId: string): Promise<void> => {
  try {
    const { content } = await fetchAllNodeChildren(nodeId);
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
