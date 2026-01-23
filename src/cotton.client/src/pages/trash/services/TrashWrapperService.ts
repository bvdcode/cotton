import type { NodeDto } from "../../../shared/api/layoutsApi";
import type { NodeFileManifestDto } from "../../../shared/api/nodesApi";

/**
 * Prefix used by the server to identify trash wrapper nodes
 */
export const TRASH_WRAPPER_PREFIX = "trash-item-";

/**
 * Service responsible for identifying and handling trash wrapper nodes.
 * 
 * Following Single Responsibility Principle (SRP):
 * - Only handles wrapper node identification logic
 * 
 * Following Open/Closed Principle (OCP):
 * - Can be extended with new wrapper identification strategies without modification
 */
export class TrashWrapperService {
  /**
   * Determines if a node is a trash wrapper node based on its name
   * 
   * @param node - The node to check
   * @returns true if the node is a wrapper node, false otherwise
   */
  public isWrapperNode(node: NodeDto): boolean {
    return node.name.startsWith(TRASH_WRAPPER_PREFIX);
  }

  /**
   * Determines if a file's owner node is a trash wrapper
   * This is used to identify files that should be treated specially in trash
   * 
   * @param file - The file to check
   * @param nodes - Available nodes to search for the parent
   * @returns true if the file belongs to a wrapper node
   */
  public isFileInWrapper(
    file: NodeFileManifestDto,
    nodes: NodeDto[],
  ): boolean {
    const ownerNode = nodes.find((n) => n.id === file.ownerId);
    return ownerNode ? this.isWrapperNode(ownerNode) : false;
  }

  /**
   * Extracts the original item ID from a wrapper node name
   * Format: "trash-item-{originalId}"
   * 
   * @param wrapperNode - The wrapper node
   * @returns The original item ID or null if not a valid wrapper
   */
  public extractOriginalId(wrapperNode: NodeDto): string | null {
    if (!this.isWrapperNode(wrapperNode)) {
      return null;
    }

    const id = wrapperNode.name.substring(TRASH_WRAPPER_PREFIX.length);
    return id || null;
  }
}

/**
 * Singleton instance of the TrashWrapperService
 */
export const trashWrapperService = new TrashWrapperService();
