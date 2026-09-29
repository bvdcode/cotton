import { useMemo, useState } from "react";
import type { GridFilterModel, GridSortModel } from "@mui/x-data-grid";
import { folderListingOptions } from "../utils/folderListingOptions";

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
      saved?.nodeId === nodeId && saved.namesEnabled === namesEnabled
        ? saved
        : {
            nodeId,
            namesEnabled,
            page: 0,
            sortModel: [],
            filterModel: { items: [] },
          },
    [saved, nodeId, namesEnabled],
  );
  const sortModel = useMemo(
    () =>
      state.sortModel.filter((item) => item.field !== "name" || namesEnabled),
    [state.sortModel, namesEnabled],
  );
  const filterModel = useMemo(
    () => ({
      ...state.filterModel,
      items: state.filterModel.items.filter(
        (item) => item.field !== "name" || namesEnabled,
      ),
    }),
    [state.filterModel, namesEnabled],
  );
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
