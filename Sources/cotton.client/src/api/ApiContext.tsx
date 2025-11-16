import { useMemo } from "react";
import { useAxios } from "@bvdcode/react-kit";
import { ApiService as ApiServiceImpl } from "./ApiService";
import type { ApiService } from "./ApiService";

// Simple hook: relies on AppShell's internal AxiosProvider
export function useApi(): ApiService {
  const axios = useAxios();
  const svc = useMemo(() => new ApiServiceImpl(() => axios), [axios]);
  return svc;
}
