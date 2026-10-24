import type { GridComparatorFn, GridSortDirection } from "@mui/x-data-grid";

export function createSizeComparator(folderIds: readonly string[]) {
  const folders = new Set(folderIds);
  return (
    direction: GridSortDirection,
  ): GridComparatorFn<number | null> | undefined => {
    let sign: number;
    switch (direction) {
      case "asc":
        sign = 1;
        break;
      case "desc":
        sign = -1;
        break;
      case null:
      case undefined:
        return undefined;
    }
    return (left, right, leftCell, rightCell) => {
      const leftId = String(leftCell.id);
      const rightId = String(rightCell.id);
      if (leftId === "__new_folder__" || rightId === "__new_folder__") {
        return (
          Number(rightId === "__new_folder__") -
          Number(leftId === "__new_folder__")
        );
      }
      const group = Number(folders.has(rightId)) - Number(folders.has(leftId));
      if (group !== 0) {
        return group;
      }
      if (left === null || right === null) {
        return Number(left === null) - Number(right === null);
      }
      return sign * (left - right);
    };
  };
}
