import { Box } from "@mui/material";
import { AppShell, type TokenPair, type UserInfo } from "@bvdcode/react-kit";

function App() {
  return (
    <Box>
      <AppShell
        appName="Cotton"
        authConfig={{
          login: async (credentials, axiosInstance) => {
            const response = await axiosInstance.post<TokenPair>(
              "http://localhost:5182/api/v1/auth/login",
              credentials,
            );
            return response.data;
          },
          getUserInfo: async (axiosInstance) => {
            const response = await axiosInstance.get<UserInfo>(
              "http://localhost:5182/api/v1/users/me",
            );
            return response.data;
          },
          refreshToken: async (refreshToken, axiosInstance) => {
            const response = await axiosInstance.post<TokenPair>(
              "http://localhost:5182/api/v1/auth/refresh",
              { refreshToken },
            );
            return response.data;
          },
          logout: async (refreshToken, axiosInstance) => {
            if (refreshToken) {
              await axiosInstance.post(
                "http://localhost:5182/api/v1/auth/revoke",
                { refreshToken },
              );
            }
          },
        }}
        pages={[
          {
            route: "/",
            name: "Home",
            component: (
              <div>
                Home Page Content <a href="/dawdaw">no</a>
              </div>
            ),
            icon: <div>🏠</div>,
          },
          {
            route: "/files",
            name: "Files",
            component: (
              <Box display="flex" width="100%" height="100%" bgcolor="red">
                Test
              </Box>
            ),
            icon: <div>📁</div>,
          },
        ]}
      />
    </Box>
  );
}

export default App;
