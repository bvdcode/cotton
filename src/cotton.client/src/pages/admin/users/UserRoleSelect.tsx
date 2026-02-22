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

function isUserRole(value: number): value is UserRole {
  return value === UserRole.User || value === UserRole.Admin;
}

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
    (e: SelectChangeEvent<UserRole>, _child: React.ReactNode) => {
      const rawValue = e.target.value;
      const numericValue =
        typeof rawValue === "number" ? rawValue : Number(rawValue);

      if (!Number.isFinite(numericValue)) return;
      if (!isUserRole(numericValue)) return;

      onChange(numericValue);
    },
    [onChange],
  );

  return (
    <FormControl fullWidth>
      <InputLabel id={labelId}>{t("users.create.role")}</InputLabel>
      <Select<UserRole>
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
