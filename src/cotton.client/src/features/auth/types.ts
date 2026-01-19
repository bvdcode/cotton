/**
 * User information interface
 */
export interface User {
  id: string;
  role: UserRole;
  username: string;
  pictureUrl?: string;
  displayName?: string;
  createdAt: string;
  updatedAt: string;
  /** 2FA (TOTP) */
  isTotpEnabled?: boolean;
  /** ISO string from backend (DateTime) */
  totpEnabledAt?: string | null;
  totpFailedAttempts?: number;
}

/**
 * Auth context state
 */
export interface AuthState {
  user: User | null; // optional snapshot for UI
  isAuthenticated: boolean; // gate-only flag
  isInitializing: boolean; // boot spinner
  refreshEnabled: boolean; // blocks refresh after explicit logout
  hydrated: boolean; // store hydration complete
  hasChecked: boolean; // at least one auth-check attempted this session
}

/**
 * Auth context value with state and actions
 */
export interface AuthContextValue extends AuthState {
  ensureAuth: () => Promise<void>;
  setAuthenticated: (value: boolean, user?: User | null) => void;
  logout: () => Promise<void>;
}

export const UserRole = {
  User: 0,
  Admin: 1,
} as const;

export type UserRole = (typeof UserRole)[keyof typeof UserRole];
