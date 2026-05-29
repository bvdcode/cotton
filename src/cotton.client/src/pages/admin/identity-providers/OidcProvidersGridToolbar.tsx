import AddIcon from "@mui/icons-material/Add";
import InfoOutlinedIcon from "@mui/icons-material/InfoOutlined";
import { Tooltip } from "@mui/material";
import { ToolbarButton } from "@mui/x-data-grid";
import { DOCUMENTATION_LINKS } from "@shared/config/documentationLinks";
import { AdminGridToolbar } from "../components/AdminGridToolbar";

interface OidcProvidersGridToolbarProps {
  createLabel: string;
  docsLabel: string;
  loading: boolean;
  refreshLabel: string;
  onCreate: () => void;
  onRefresh: () => void;
}

const openOidcSetupDocs = (): void => {
  window.open(DOCUMENTATION_LINKS.oidcSetup, "_blank", "noopener,noreferrer");
};

export const OidcProvidersGridToolbar = ({
  docsLabel,
  ...props
}: OidcProvidersGridToolbarProps) => (
  <AdminGridToolbar
    {...props}
    createIcon={<AddIcon fontSize="small" />}
    extraControls={
      <Tooltip title={docsLabel}>
        <ToolbarButton aria-label={docsLabel} onClick={openOidcSetupDocs}>
          <InfoOutlinedIcon fontSize="small" />
        </ToolbarButton>
      </Tooltip>
    }
  />
);
