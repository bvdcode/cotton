import { Badge, Tooltip } from "@mui/material";
import {
  ColumnsPanelTrigger,
  FilterPanelTrigger,
  ToolbarButton,
  useGridRootProps,
} from "@mui/x-data-grid";
import { GridToolbar } from "@mui/x-data-grid/internals";
import PersonAddAltIcon from "@mui/icons-material/PersonAddAlt";
import RefreshIcon from "@mui/icons-material/Refresh";

export interface UsersGridToolbarProps {
  createLabel: string;
  loading: boolean;
  refreshLabel: string;
  onCreate: () => void;
  onRefresh: () => void;
}

export const UsersGridToolbar = ({
  createLabel,
  loading,
  refreshLabel,
  onCreate,
  onRefresh,
}: UsersGridToolbarProps) => {
  const rootProps = useGridRootProps();
  const ColumnSelectorIcon = rootProps.slots.columnSelectorIcon;
  const FilterIcon = rootProps.slots.openFilterButtonIcon;

  return (
    <GridToolbar
      mainControls={
        <>
          <Tooltip title={refreshLabel}>
            <span>
              <ToolbarButton
                aria-label={refreshLabel}
                onClick={onRefresh}
                disabled={loading}
              >
                <RefreshIcon fontSize="small" />
              </ToolbarButton>
            </span>
          </Tooltip>

          <Tooltip title={createLabel}>
            <ToolbarButton aria-label={createLabel} onClick={onCreate}>
              <PersonAddAltIcon fontSize="small" />
            </ToolbarButton>
          </Tooltip>

          {!rootProps.disableColumnSelector && (
            <Tooltip title={rootProps.localeText.toolbarColumns}>
              <ColumnsPanelTrigger render={<ToolbarButton />}>
                <ColumnSelectorIcon fontSize="small" />
              </ColumnsPanelTrigger>
            </Tooltip>
          )}

          {!rootProps.disableColumnFilter && (
            <Tooltip title={rootProps.localeText.toolbarFilters}>
              <FilterPanelTrigger
                render={(triggerProps, state) => (
                  <ToolbarButton
                    {...triggerProps}
                    color={state.filterCount > 0 ? "primary" : "default"}
                  >
                    <Badge
                      badgeContent={state.filterCount}
                      color="primary"
                      variant="dot"
                    >
                      <FilterIcon fontSize="small" />
                    </Badge>
                  </ToolbarButton>
                )}
              />
            </Tooltip>
          )}
        </>
      }
    />
  );
};
