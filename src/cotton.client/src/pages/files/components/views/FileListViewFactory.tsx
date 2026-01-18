import React from "react";
import { InterfaceLayoutType } from "../../../../shared/api/layoutsApi";
import { TilesView } from "./TilesView";
import { ListView } from "./ListView";
import type { IFileListView } from "../../types/FileListViewTypes";

/**
 * FileListViewFactory
 * 
 * Factory component that creates the appropriate view component based on the layout type.
 * Implements the Factory Pattern and Open/Closed Principle (OCP):
 * - Open for extension: New view types can be added easily
 * - Closed for modification: Existing code doesn't need to change when adding new views
 */
interface FileListViewFactoryProps extends IFileListView {
  layoutType: InterfaceLayoutType;
}

export const FileListViewFactory: React.FC<FileListViewFactoryProps> = ({
  layoutType,
  ...viewProps
}) => {
  // Factory method: Returns the appropriate view component
  switch (layoutType) {
    case InterfaceLayoutType.Tiles:
      return <TilesView {...viewProps} />;
    
    case InterfaceLayoutType.List:
      return <ListView {...viewProps} />;
    
    default:
      // Fallback to Tiles view if unknown type
      return <TilesView {...viewProps} />;
  }
};
