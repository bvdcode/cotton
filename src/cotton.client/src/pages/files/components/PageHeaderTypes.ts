import type React from "react";
import type { ReactElement } from "react";
import type { FileBreadcrumbs } from "./FileBreadcrumbs";
import type { FileBrowserViewMode } from "@shared/utils/viewMode";
import type {
  FileBreadcrumb,
  FileListStats,
} from "@shared/types/FileListViewTypes";

export interface PageHeaderActionItem {
  key: string;
  icon: ReactElement;
  title: string;
  onClick: (event?: React.MouseEvent<HTMLElement>) => void;
  disabled?: boolean;
  color?: "primary" | "secondary" | "error";
  active?: boolean;
  /** Optional DnD drop target handlers (e.g. Up button accepts move drop). */
  onDragOver?: (event: React.DragEvent<HTMLElement>) => void;
  onDragLeave?: (event: React.DragEvent<HTMLElement>) => void;
  onDrop?: (event: React.DragEvent<HTMLElement>) => void;
  dropActive?: boolean;
}

export interface PageHeaderProps {
  loading: boolean;
  breadcrumbs: FileBreadcrumb[];
  onNavigateBreadcrumb?: (breadcrumbIndex: number) => void;
  stats: FileListStats;
  isSizeCalculating?: boolean;
  viewMode: FileBrowserViewMode;
  canGoUp: boolean;
  onGoUp: () => void;
  onHomeClick: () => void;
  onViewModeCycle: () => void;
  showViewModeToggle?: boolean;
  statsNamespace?: string;

  // Optional actions
  showUpload?: boolean;
  showNewFile?: boolean;
  showNewFolder?: boolean;
  onUploadClick?: () => void;
  onNewFileClick?: () => void;
  onNewFolderClick?: () => void;
  isCreatingFile?: boolean;
  isCreatingFolder?: boolean;

  // Selection mode
  selectionMode?: boolean;
  selectedCount?: number;
  onToggleSelectionMode?: () => void;
  onSelectAll?: () => void;
  onDeselectAll?: () => void;

  // Custom actions rendered in overflow-aware action bar
  customActionItems?: PageHeaderActionItem[];
  primaryActionItems?: PageHeaderActionItem[];

  /** Optional drop handlers for breadcrumbs (move drag target). */
  breadcrumbsDropHandlers?: React.ComponentProps<
    typeof FileBreadcrumbs
  >["dropHandlers"];
  /** Optional drop handlers attached to the "Go up" action. */
  goUpDropHandlers?: {
    onDragOver: (event: React.DragEvent<HTMLElement>) => void;
    onDragLeave: (event: React.DragEvent<HTMLElement>) => void;
    onDrop: (event: React.DragEvent<HTMLElement>) => void;
    active: boolean;
  };
}
