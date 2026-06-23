import React from "react";
import { InterfaceLayoutType } from "../../../../shared/api/layoutsApi";
import { TilesView } from "./TilesView";
import { ListView } from "./ListView";
import type { IFileListView } from "@shared/types/FileListViewTypes";

/**
 * Renders the appropriate file list view component based on the layout type.
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
