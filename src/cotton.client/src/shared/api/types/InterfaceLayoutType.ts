/**
 * Enum representing the UI layout type for a node.
 * Matches the backend InterfaceLayoutType enum.
 */
export const InterfaceLayoutType = {
  /** Tiles/Grid layout - displays items as cards in a grid */
  Tiles: 0,
  /** List layout - displays items in a table/list format */
  List: 1,
} as const;

export type InterfaceLayoutType = typeof InterfaceLayoutType[keyof typeof InterfaceLayoutType];
