import type { NodeDto } from "../../../shared/api/layoutsApi";
import type {
  NodeFileManifestDto,
  NodeContentDto,
} from "../../../shared/api/nodesApi";
import { trashWrapperService } from "./TrashWrapperService";

/**
 * Interface for transformed trash content with metadata about wrappers
 */
export interface TransformedTrashContent {
  /**
   * Nodes to display (unwrapped if they were in wrappers)
   */
  nodes: NodeDto[];

  /**
   * Files to display
   */
  files: NodeFileManifestDto[];

  /**
   * Mapping from displayed node/file ID to its wrapper node ID (if any)
   * This is used for deletion - we delete the wrapper, not the content
   */
  wrapperMap: Map<string, string>;
}

/**
 * Service responsible for transforming trash content by unwrapping wrapper nodes.
 * 
 * Following Single Responsibility Principle (SRP):
 * - Only handles content transformation logic
 * 
 * Following Dependency Inversion Principle (DIP):
 * - Depends on TrashWrapperService abstraction
 * 
 * When trash items are deleted, they are wrapped in a folder with name "trash-item-{id}".
 * This service unwraps that content for display, while maintaining the mapping
 * for proper deletion (we delete the wrapper, not the unwrapped content).
 */
export class TrashContentTransformer {
  /**
   * Transforms node content by unwrapping wrapper nodes
   * 
   * @param content - The raw content from the API (with depth=1)
   * @returns Transformed content with unwrapped items and wrapper mapping
   */
  public transformContent(content: NodeContentDto): TransformedTrashContent {
    const displayNodes: NodeDto[] = [];
    const displayFiles: NodeFileManifestDto[] = [];
    const wrapperMap = new Map<string, string>();

    // First pass: identify wrapper nodes
    const wrapperNodeIds = new Set<string>();
    for (const node of content.nodes ?? []) {
      if (trashWrapperService.isWrapperNode(node)) {
        wrapperNodeIds.add(node.id);
        // Map the wrapper to itself for direct deletion
        wrapperMap.set(node.id, node.id);
      }
    }

    // Second pass: process nodes - skip wrappers, but include their children
    for (const node of content.nodes ?? []) {
      if (wrapperNodeIds.has(node.id)) {
        // This is a wrapper node - don't display it
        continue;
      }

      // Check if this node is inside a wrapper (parentId is a wrapper)
      if (node.parentId && wrapperNodeIds.has(node.parentId)) {
        // This node is a child of a wrapper - display it but map it for deletion
        displayNodes.push(node);
        wrapperMap.set(node.id, node.parentId);
      } else {
        // Regular node not in a wrapper
        displayNodes.push(node);
      }
    }

    // Third pass: process files
    for (const file of content.files ?? []) {
      // Check if file's owner is a wrapper node
      if (wrapperNodeIds.has(file.ownerId)) {
        // File is directly inside a wrapper
        displayFiles.push(file);
        wrapperMap.set(file.id, file.ownerId);
      } else {
        // Regular file or file in a regular folder
        displayFiles.push(file);
      }
    }

    console.log(
      `Transformed trash content: ${wrapperNodeIds.size} wrappers, ${displayNodes.length} nodes, ${displayFiles.length} files`,
    );

    return {
      nodes: displayNodes,
      files: displayFiles,
      wrapperMap,
    };
  }

  /**
   * Gets the wrapper ID for an item (node or file) if it exists
   * 
   * @param itemId - The ID of the node or file
   * @param wrapperMap - The wrapper mapping from transformContent
   * @returns The wrapper node ID if item is wrapped, otherwise the original item ID
   */
  public getDeleteTarget(
    itemId: string,
    wrapperMap: Map<string, string>,
  ): string {
    return wrapperMap.get(itemId) ?? itemId;
  }
}

/**
 * Singleton instance of the TrashContentTransformer
 */
export const trashContentTransformer = new TrashContentTransformer();
