import type { NodeDto } from "../api/layoutsApi";
import {
  applyDisplayMetaToNode,
  applyDisplayMetaToNodes,
} from "./folderDisplayMeta";
import { useDisplayProjection } from "./useDisplayProjection";

async function projectNode(node: NodeDto | null): Promise<NodeDto | null> {
  return node ? applyDisplayMetaToNode(node) : null;
}

export function useDecryptedNode(node: NodeDto | null): NodeDto | null {
  return useDisplayProjection(node, projectNode);
}

export function useDecryptedNodes(nodes: NodeDto[]): NodeDto[] {
  return useDisplayProjection(nodes, applyDisplayMetaToNodes);
}
