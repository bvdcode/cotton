import { Badge, Tooltip } from "@mui/material";
import AddIcon from "@mui/icons-material/Add";
import RefreshIcon from "@mui/icons-material/Refresh";
import {
  ColumnsPanelTrigger,
  FilterPanelTrigger,
  ToolbarButton,
  useGridRootProps,
} from "@mui/x-data-grid";
import { GridToolbar } from "@mui/x-data-grid/internals";

interface OidcProvidersGridToolbarProps {
  createLabel: string;
  loading: boolean;
  refreshLabel: string;
  onCreate: () => void;
  onRefresh: () => void;
}

export const OidcProvidersGridToolbar = ({
  createLabel,
  loading,
  refreshLabel,
  onCreate,
  onRefresh,
}: OidcProvidersGridToolbarProps) => {
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
              <AddIcon fontSize="small" />
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
