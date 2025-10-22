import { useAuth } from "../stores/authStore.ts";

export async function apiFetch(input: string, init: RequestInit = {}): Promise<Response> {
  const { token } = useAuth.getState();
  const headers = new Headers(init.headers || {});
  if (token) {
    headers.set("Authorization", `Bearer ${token}`);
  }
  return fetch(input, { ...init, headers });
}
