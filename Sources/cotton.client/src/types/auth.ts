import type { ReactNode } from "react";
import type { AxiosInstance } from "axios";

export type TranslationResources = {
  [language: string]: {
    [namespace: string]: Record<string, any>;
  };
};

export type ReactKitProps = {
  appName: string;
  logoUrl?: string;
  baseURL?: string;
  basename?: string;
  pages: ReactKitPage[];
  authConfig?: AuthConfig;
  translations?: TranslationResources;
  themeOverrides?: ThemeOverrides;
  contentMaxWidth?: string | number;
  renderLoginPage?: (props: ReactKitProps) => ReactNode;
  renderNavigationBar?: (props: ReactKitProps) => ReactNode;
};

export type ReactKitPage = {
  route: string;
  url?: string;
  name?: string;
  icon?: ReactNode;
  component: ReactNode;
};

export type ThemeMode = "light" | "dark";

export type SimplePaletteOverrides = {
  primary?: string; // hex or css color
  secondary?: string; // hex or css color
  background?: string; // base page background (maps to palette.background.default)
  text?: string; // base text color (maps to palette.text.primary)
};

export type ThemeOverrides = {
  light?: SimplePaletteOverrides;
  dark?: SimplePaletteOverrides;
};

export interface TokenPair {
  accessToken: string;
  refreshToken: string;
}

export interface UserInfo {
  id: string;
  username: string;
  displayName: string;
  avatarUrl?: string;
}

export interface AuthConfig {
  usernamePattern?: RegExp;
  login?: (
    credentials: LoginCredentials,
    axiosInstance: AxiosInstance,
  ) => Promise<TokenPair>;
  refreshToken?: (
    refreshToken: string,
    axiosInstance: AxiosInstance,
  ) => Promise<TokenPair>;
  getUserInfo?: (axiosInstance: AxiosInstance) => Promise<UserInfo>;
  logout?: (
    refreshToken: string | null,
    axiosInstance: AxiosInstance,
  ) => Promise<void> | void;
}

export interface LoginCredentials {
  username: string;
  password: string;
}

export class AuthError extends Error {
  constructor(message: string, public code?: string) {
    super(message);
    this.name = "AuthError";
  }
}
