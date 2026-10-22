import { useMemo, useState } from "react";
import type { GridFilterModel, GridSortModel } from "@mui/x-data-grid";
import { folderListingOptions } from "../utils/folderListingOptions";

const EMPTY_SORT_MODEL: GridSortModel = [];
const EMPTY_FILTER_MODEL: GridFilterModel = { items: [] };

interface FolderListControls {
  nodeId: string | null;
  namesEnabled: boolean;
  page: number;
  sortModel: GridSortModel;
  filterModel: GridFilterModel;
}

export function useFolderListControls(
  nodeId: string | null,
  namesEnabled: boolean,
) {
  const [saved, setSaved] = useState<FolderListControls | null>(null);
  const state = useMemo<FolderListControls>(
    () =>
      saved?.nodeId === nodeId &&
      (saved.namesEnabled === namesEnabled || namesEnabled)
        ? { ...saved, namesEnabled }
        : {
            nodeId,
            namesEnabled,
            page: 0,
            sortModel: EMPTY_SORT_MODEL,
            filterModel: EMPTY_FILTER_MODEL,
          },
    [saved, nodeId, namesEnabled],
  );
  const sortModel = useMemo(() => {
    if (
      namesEnabled ||
      !state.sortModel.some((item) => item.field === "name")
    ) {
      return state.sortModel;
    }
    return state.sortModel.filter((item) => item.field !== "name");
  }, [state.sortModel, namesEnabled]);
  const filterModel = useMemo(() => {
    if (
      namesEnabled ||
      !state.filterModel.items.some((item) => item.field === "name")
    ) {
      return state.filterModel;
    }
    return {
      ...state.filterModel,
      items: state.filterModel.items.filter((item) => item.field !== "name"),
    };
  }, [state.filterModel, namesEnabled]);
  return {
    page: state.page,
    setPage: (page: number) => setSaved({ ...state, page }),
    listing: folderListingOptions(sortModel, filterModel),
    query: {
      namesEnabled,
      sortModel,
      filterModel,
      onSortModelChange: (model: GridSortModel) =>
        setSaved({ ...state, page: 0, sortModel: model }),
      onFilterModelChange: (model: GridFilterModel) =>
        setSaved({ ...state, page: 0, filterModel: model }),
    },
  };
}
