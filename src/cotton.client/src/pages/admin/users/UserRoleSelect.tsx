import * as React from "react";
import {
  FormControl,
  InputLabel,
  MenuItem,
  Select,
  type SelectChangeEvent,
} from "@mui/material";
import { useTranslation } from "react-i18next";
import { UserRole } from "../../../features/auth/types";

interface UserRoleSelectProps {
  labelId: string;
  value: UserRole;
  disabled?: boolean;
  onChange: (role: UserRole) => void;
}

export const UserRoleSelect: React.FC<UserRoleSelectProps> = ({
  labelId,
  value,
  disabled = false,
  onChange,
}) => {
  const { t } = useTranslation(["admin", "common"]);

  const roleLabel = React.useCallback(
    (role: UserRole): string => {
      if (role === UserRole.Admin) return t("roles.admin");
      if (role === UserRole.User) return t("roles.user");
      return t("roles.unknown");
    },
    [t],
  );

  const handleChange = React.useCallback(
    (e: SelectChangeEvent) => {
      onChange(e.target.value as UserRole);
    },
    [onChange],
  );

  return (
    <FormControl fullWidth>
      <InputLabel id={labelId}>{t("users.create.role")}</InputLabel>
      <Select
        labelId={labelId}
        label={t("users.create.role")}
        value={value}
        onChange={handleChange}
        disabled={disabled}
      >
        <MenuItem value={UserRole.User}>{roleLabel(UserRole.User)}</MenuItem>
        <MenuItem value={UserRole.Admin}>{roleLabel(UserRole.Admin)}</MenuItem>
      </Select>
    </FormControl>
  );
};
