import PersonAddAltIcon from "@mui/icons-material/PersonAddAlt";
import { AdminGridToolbar } from "../../components/AdminGridToolbar";

export interface UsersGridToolbarProps {
  createLabel: string;
  loading: boolean;
  refreshLabel: string;
  onCreate: () => void;
  onRefresh: () => void;
}

export const UsersGridToolbar = (props: UsersGridToolbarProps) => (
  <AdminGridToolbar
    {...props}
    createIcon={<PersonAddAltIcon fontSize="small" />}
  />
);
