import { ru, en } from "./locales";
import { Box } from "@mui/material";
import FilesPage from "./pages/FilesPage";
import { AppShell, type TokenPair, type UserInfo } from "@bvdcode/react-kit";
import type { AuthUser } from "./api";

function App() {
  return (
    <Box>
      <AppShell
        appName="Cotton"
        logoUrl="/icon.png"
        translations={{
          en: { translation: en },
          ru: { translation: ru },
        }}
        authConfig={{
          login: async (credentials, axiosInstance) => {
            const response = await axiosInstance.post<TokenPair>(
              "/api/v1/auth/login",
              credentials,
            );
            return response.data;
          },
          getUserInfo: async (axiosInstance) => {
            const response = await axiosInstance.get<AuthUser>(
              "/api/v1/users/me",
            );
            return {
              ...response.data,
              displayName: response.data.username,
            } as UserInfo;
          },
          refreshToken: async (refreshToken, axiosInstance) => {
            const response = await axiosInstance.post<TokenPair>(
              "/api/v1/auth/refresh",
              { refreshToken },
            );
            return response.data;
          },
          logout: async (refreshToken, axiosInstance) => {
            if (refreshToken) {
              await axiosInstance.post("/api/v1/auth/revoke", { refreshToken });
            }
          },
        }}
        pages={[
          {
            route: "/",
            name: "Files",
            component: <FilesPage />,
            icon: <div>📁</div>,
          },
        ]}
      />
    </Box>
  );
}

export default App;
