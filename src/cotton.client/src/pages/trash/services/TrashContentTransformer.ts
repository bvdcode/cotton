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

    // Process nodes
    for (const node of content.nodes ?? []) {
      if (trashWrapperService.isWrapperNode(node)) {
        // This is a wrapper node - we should have gotten its children via depth=1
        // The children should be in the content as nested items
        // But since API returns flat structure with depth=1, 
        // we need to identify which nodes/files belong to this wrapper
        
        // For now, we'll keep wrapper nodes visible but mark them
        // The actual unwrapping will happen when we process the full nested structure
        
        // Skip wrapper nodes from display - their content will be shown instead
        // Store the wrapper ID for future deletion
        wrapperMap.set(node.id, node.id);
      } else {
        // Regular node - display as-is
        displayNodes.push(node);
      }
    }

    // Process files - check if they're owned by a wrapper node
    for (const file of content.files ?? []) {
      const ownerIsWrapper = trashWrapperService.isFileInWrapper(
        file,
        content.nodes ?? [],
      );

      if (ownerIsWrapper) {
        // File is inside a wrapper - map it to its wrapper for deletion
        wrapperMap.set(file.id, file.ownerId);
      }

      // Display all files
      displayFiles.push(file);
    }

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
