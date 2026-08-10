import type { SvgIconProps } from "@mui/material/SvgIcon";
import type { ComponentType } from "react";

export const ADMIN_NAV_WIDTH = {
  collapsed: 64,
  expanded: 248,
} as const;

type AdminMenuItemId =
  | "generalSettings"
  | "users"
  | "privacySettings"
  | "securityDiagnostics"
  | "identityProviders"
  | "storageSettings"
  | "storageStatistics"
  | "notificationsSettings"
  | "databaseBackup";

type AdminMenuSectionId = "people" | "storage" | "security" | "system";

export interface AdminMenuItem {
  id: AdminMenuItemId;
  to: string;
  title: string;
  icon: ComponentType<SvgIconProps>;
}

export interface AdminMenuSection {
  id: AdminMenuSectionId;
  title: string;
  items: AdminMenuItem[];
}
