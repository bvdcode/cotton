import React, { createContext, useContext, useMemo } from "react";
import type { PropsWithChildren } from "react";
import { useAxios } from "@bvdcode/react-kit";
import type { ApiService } from "./ApiService";
import { ApiService as ApiServiceImpl } from "./ApiService";

const ApiContext = createContext<ApiService | null>(null);

export function ApiProvider({ children }: PropsWithChildren) {
  const axios = useAxios();
  const service = useMemo(() => {
    if (!axios) return null;
    return new ApiServiceImpl(() => axios);
  }, [axios]);

  if (!service) return null; // AppShell not initialized yet
  return <ApiContext.Provider value={service}>{children}</ApiContext.Provider>;
}

export function useApi(): ApiService {
  const ctx = useContext(ApiContext);
  if (!ctx) throw new Error("ApiProvider is missing. Place the page under ApiProvider inside AppShell.");
  return ctx;
}
