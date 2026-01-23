/**
 * Barrel export for trash services
 * 
 * Following Dependency Inversion Principle (DIP):
 * - Provides a clean interface for importing services
 */

export { TrashWrapperService, trashWrapperService, TRASH_WRAPPER_PREFIX } from "./TrashWrapperService";
export { TrashContentTransformer, trashContentTransformer } from "./TrashContentTransformer";
export type { TransformedTrashContent } from "./TrashContentTransformer";
