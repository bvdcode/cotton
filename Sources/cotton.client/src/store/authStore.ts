import { create } from "zustand";
import { persist } from "zustand/middleware";
import { getAppPrefix } from "../config/appPrefix";
import type { LoginCredentials, TokenPair } from "../types";
import type { ApiService } from "../api";

interface AuthStore {
  refreshToken: string | null;
  accessToken: string | null;
  apiService: ApiService | null;
  
  setApiService: (service: ApiService) => void;
  setRefreshToken: (token: string) => void;
  setAccessToken: (token: string) => void;
  getRefreshToken: () => string | null;
  clearTokens: () => void;
  
  login: (credentials: LoginCredentials) => Promise<void>;
  logout: () => Promise<void>;
}

export const useAuthStore = create<AuthStore>()(
  persist(
    (set, get) => ({
      refreshToken: null,
      accessToken: null,
  apiService: null,
      
  setApiService: (service: ApiService) => set({ apiService: service }),
      
      setRefreshToken: (token: string) => set({ refreshToken: token }),
      
      setAccessToken: (token: string) => set({ accessToken: token }),
      
      getRefreshToken: () => get().refreshToken,
      
      clearTokens: () => set({ refreshToken: null, accessToken: null }),
      
      login: async (credentials: LoginCredentials) => {
        const { apiService } = get();
        if (!apiService) {
          throw new Error("API service is not initialized");
        }
        
        const tokens: TokenPair = await apiService.login(credentials);
        
        // Save refresh token to store (persisted)
        set({ refreshToken: tokens.refreshToken });
        
        // Save access token to memory only
        set({ accessToken: tokens.accessToken });
      },
      
      logout: async () => {
        const { apiService } = get();
        if (apiService) {
          await apiService.logout();
        }
        set({ refreshToken: null, accessToken: null });
      },
    }),
    {
      name: `${getAppPrefix()}-auth`,
      partialize: (state) => ({ 
        refreshToken: state.refreshToken 
      }),
    },
  ),
);
