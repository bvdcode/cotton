import { nodesApi, type NodeResponse } from "./nodesApi";
import { applyDisplayMetaToFiles } from "../crypto/displayMeta";

export async function fetchNodeChildren(
  nodeId: string,
  options?: Parameters<typeof nodesApi.getChildren>[1],
): Promise<NodeResponse> {
  const response = await nodesApi.getChildren(nodeId, options);
  const files = await applyDisplayMetaToFiles(response.content.files);
  return { ...response, content: { ...response.content, files } };
}

export async function fetchAllNodeChildren(
  nodeId: string,
  options?: { nodeType?: string; depth?: number },
): Promise<NodeResponse> {
  return readAllPages(nodeId, options, fetchNodeChildren);
}

export async function fetchAllRawNodeChildren(
  nodeId: string,
  options?: { nodeType?: string; depth?: number },
): Promise<NodeResponse> {
  return readAllPages(nodeId, options, nodesApi.getChildren);
}

async function readAllPages(
  nodeId: string,
  options: { nodeType?: string; depth?: number } | undefined,
  readPage: typeof fetchNodeChildren,
): Promise<NodeResponse> {
  const firstPage = await readPage(nodeId, { ...options, page: 1 });
  const content = {
    ...firstPage.content,
    nodes: [...firstPage.content.nodes],
    files: [...firstPage.content.files],
  };

  let page = 2;
  while (content.nodes.length + content.files.length < firstPage.totalCount) {
    const response = await readPage(nodeId, { ...options, page });
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
