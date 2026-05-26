import AddIcon from "@mui/icons-material/Add";
import { AdminGridToolbar } from "../components/AdminGridToolbar";

interface OidcProvidersGridToolbarProps {
  createLabel: string;
  loading: boolean;
  refreshLabel: string;
  onCreate: () => void;
  onRefresh: () => void;
}

export const OidcProvidersGridToolbar = (
  props: OidcProvidersGridToolbarProps,
) => <AdminGridToolbar {...props} createIcon={<AddIcon fontSize="small" />} />;
