import "./i18n";
import { ru, en } from "./locales";
import type { AuthUser } from "./api";
import FilesPage from "./pages/FilesPage";
import LoginPage from "./pages/LoginPage";
import { Box, Typography } from "@mui/material";
import { Folder, Home } from "@mui/icons-material";
import { AppShell, type TokenPair, type UserInfo } from "@bvdcode/react-kit";

function App() {
  return (
    <Box>
      <AppShell
        appName="Cotton"
        logoUrl="/icon.png"
        renderLoginPage={(props) => <LoginPage appProps={props} />}
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
            name: "Home",
            component: (
              <Box>
                <Typography variant="h4">Welcome to Cotton</Typography>
                <Typography variant="body1">
                  Select a page from the menu.
                </Typography>
              </Box>
            ),
            icon: <Home />,
          },
          {
            url: "/files",
            route: "/files/:nodeId?",
            name: "Files",
            component: <FilesPage />,
            icon: <Folder />,
          },
        ]}
      />
    </Box>
  );
}

export default App;
