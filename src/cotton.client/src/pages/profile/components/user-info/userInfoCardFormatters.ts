import { UserRole } from "../../../../features/auth/types";

export type RoleTranslationKey =
  | "roles.user"
  | "roles.admin"
  | "roles.unknown";

export const getRoleTranslationKey = (role: number): RoleTranslationKey => {
  switch (role) {
    case UserRole.Admin:
      return "roles.admin";
    case UserRole.User:
      return "roles.user";
    default:
      return "roles.unknown";
  }
};

export const getAvatarInitials = (args: {
  firstName?: string | null;
  lastName?: string | null;
  username?: string | null;
  email?: string | null;
}): string => {
  const first = (args.firstName ?? "").trim();
  const last = (args.lastName ?? "").trim();
  if (first && last) {
    return `${first.charAt(0)}${last.charAt(0)}`.toUpperCase();
  }

  const fallback = (args.username ?? args.email ?? "").trim();
  if (!fallback) {
    return "";
  }

  return fallback.slice(0, 2).toUpperCase();
};
