/**
 * User information interface
 */
export interface User {
  id: string;
  username: string;
  pictureUrl?: string;
  displayName?: string;
}

/**
 * Auth context state
 */
export interface AuthState {
  user: User | null; // optional snapshot for UI
  isAuthenticated: boolean; // gate-only flag
  isInitializing: boolean; // boot spinner
}

/**
 * Auth context value with state and actions
 */
export interface AuthContextValue extends AuthState {
  setAuthenticated: (value: boolean, user?: User | null) => void;
  logout: () => Promise<void>;
}
