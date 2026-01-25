/**
 * Search API Types
 * 
 * Type definitions for search functionality
 * Following Interface Segregation: Each interface has a single, well-defined purpose
 */

import type { Guid, NodeDto } from "./layoutsApi";
import type { NodeFileManifestDto } from "./nodesApi";

/**
 * Search result item - can be either a node (folder) or file
 */
export interface SearchResultDto {
  /** Array of matching nodes (folders) */
  nodes: NodeDto[];
  /** Array of matching files */
  files: NodeFileManifestDto[];
  /** Total number of results (for pagination) */
  totalCount: number;
  /** Current page number */
  page: number;
  /** Number of items per page */
  pageSize: number;
}

/**
 * Parameters for search request
 */
export interface SearchParams {
  /** Layout ID to search within */
  layoutId: Guid;
  /** Search query string */
  query: string;
  /** Page number (1-based) */
  page?: number;
  /** Items per page */
  pageSize?: number;
}

/**
 * Union type for search result items
 * Used for rendering mixed results
 */
export type SearchResultItem = 
  | { type: 'node'; data: NodeDto }
  | { type: 'file'; data: NodeFileManifestDto };
