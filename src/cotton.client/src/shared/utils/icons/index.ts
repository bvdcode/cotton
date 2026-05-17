/**
 * Icon System for Files and Folders
 * 
 * Architecture:
 * - Single Responsibility: Each function handles one type of icon
 * - Open/Closed: Easy to extend with new file types without modifying existing code
 * - Liskov Substitution: All icon functions return compatible types
 * - Interface Segregation: Separate functions for folders vs files
 * - Dependency Inversion: Components depend on ReactNode | string abstraction
 */

export { getFolderIcon } from './FolderIcon';
export { getFileIcon } from './FileIcon';
export type { IconResult } from './types';
