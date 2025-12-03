import React, { createContext, useContext, PropsWithChildren, useMemo } from "react";
import type { AxiosInstance } from "axios";
import { useAuthStore } from "../store/authStore";

const AxiosContext = createContext<AxiosInstance | undefined>(undefined);

export function AxiosProvider({ children }: PropsWithChildren) {
  const apiService = useAuthStore((s) => s.apiService);
  const axios = useMemo(() => apiService?.getAxios(), [apiService]);
  if (!axios) return null;
  return <AxiosContext.Provider value={axios}>{children}</AxiosContext.Provider>;
}

export function useAxios(): AxiosInstance {
  const ctx = useContext(AxiosContext);
  if (!ctx) {
    throw new Error("useAxios must be used within <AxiosProvider> (inside ProtectedContent)");
  }
  return ctx;
}
