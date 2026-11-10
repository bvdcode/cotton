import * as React from "react";
import type { Guid } from "@shared/api/layoutsApi";

interface BreadcrumbNode {
  id: Guid;
  name: string;
}

type BreadcrumbState = {
  rootNodeId: Guid;
  rootName: string;
  breadcrumbs: BreadcrumbNode[];
};

const createRootBreadcrumbState = (
  rootNodeId: Guid,
  rootName: string,
): BreadcrumbState => ({
  rootNodeId,
  rootName,
  breadcrumbs: [{ id: rootNodeId, name: rootName }],
});

export const useSharedFolderNavigation = (
  rootNodeId: Guid,
  rootName: string,
) => {
  const rootBreadcrumbs = React.useMemo<BreadcrumbNode[]>(
    () => [{ id: rootNodeId, name: rootName }],
    [rootName, rootNodeId],
  );
  const [breadcrumbState, setBreadcrumbState] = React.useState<BreadcrumbState>(
    () => createRootBreadcrumbState(rootNodeId, rootName),
  );
  const breadcrumbs =
    breadcrumbState.rootNodeId === rootNodeId &&
    breadcrumbState.rootName === rootName
      ? breadcrumbState.breadcrumbs
      : rootBreadcrumbs;
  const currentNode = React.useMemo(
    () => breadcrumbs[breadcrumbs.length - 1] ?? null,
    [breadcrumbs],
  );
  const handleOpenFolder = React.useCallback(
    (folderId: Guid, folderName: string) => {
      setBreadcrumbState((current) => {
        const base =
          current.rootNodeId === rootNodeId && current.rootName === rootName
            ? current
            : createRootBreadcrumbState(rootNodeId, rootName);
        return {
          ...base,
          breadcrumbs: [
            ...base.breadcrumbs,
            { id: folderId, name: folderName },
          ],
        };
      });
    },
    [rootName, rootNodeId],
  );

  const handleNavigateBreadcrumb = React.useCallback(
    (index: number) => {
      setBreadcrumbState((current) => {
        const base =
          current.rootNodeId === rootNodeId && current.rootName === rootName
            ? current
            : createRootBreadcrumbState(rootNodeId, rootName);
        return {
          ...base,
          breadcrumbs: base.breadcrumbs.slice(0, index + 1),
        };
      });
    },
    [rootName, rootNodeId],
  );

  return {
    breadcrumbs,
    currentNode,
    handleOpenFolder,
    handleNavigateBreadcrumb,
  };
};
