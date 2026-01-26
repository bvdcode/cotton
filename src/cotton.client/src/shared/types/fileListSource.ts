import type { FileSystemTile } from "../../pages/files/types/FileListViewTypes";

export interface FileListSource {
  loading: boolean;
  error: string | null;
  tiles: FileSystemTile[];
  totalCount?: number;
  refresh?: () => void | Promise<void>;
}

export interface FileListPagination {
  page: number;
  pageSize: number;
  totalCount: number;
  loading: boolean;
  onPageChange: (page: number) => void;
  onPageSizeChange: (pageSize: number) => void;
}

export interface FileListBreadcrumb {
  id?: string;
  name: string;
}

export interface FileListStats {
  folders: number;
  files: number;
}

export interface FileListActions {
  canCreateFolder?: boolean;
  canUpload?: boolean;
  canDelete?: boolean;
  canRestore?: boolean;
  customActions?: React.ReactNode;
}
