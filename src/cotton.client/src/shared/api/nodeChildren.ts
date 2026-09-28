import { nodesApi, type NodeResponse } from "./nodesApi";

export async function fetchAllNodeChildren(
  nodeId: string,
  options?: { nodeType?: string; depth?: number },
): Promise<NodeResponse> {
  const firstPage = await nodesApi.getChildren(nodeId, { ...options, page: 1 });
  const content = {
    ...firstPage.content,
    nodes: [...firstPage.content.nodes],
    files: [...firstPage.content.files],
  };

  let page = 2;
  while (content.nodes.length + content.files.length < firstPage.totalCount) {
    const response = await nodesApi.getChildren(nodeId, { ...options, page });
    if (
      response.content.nodes.length === 0 &&
      response.content.files.length === 0
    ) {
      break;
    }

    content.nodes.push(...response.content.nodes);
    content.files.push(...response.content.files);
    page += 1;
  }

  return { content, totalCount: firstPage.totalCount };
}
