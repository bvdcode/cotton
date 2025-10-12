import {
  Route,
  Routes,
  Navigate,
  BrowserRouter as Router,
} from "react-router-dom";
import { Box } from "@mui/material";
import "react-toastify/dist/ReactToastify.css";
import { ConfirmProvider } from "material-ui-confirm";
import AppThemeProvider from "./providers/ThemeProvider.tsx";
import { AppLayout, ProtectedRoute, FilesPage } from "./components/index.ts";
// i18n is initialized in main.tsx

function App() {
  return (
    <Box sx={{ position: "fixed", inset: 0 }}>
      <AppThemeProvider>
        <ConfirmProvider>
          <Router>
            <Routes>
              <Route path="/login" element={<>Login</>} />
              <Route
                path="/app"
                element={
                  <ProtectedRoute>
                    <AppLayout />
                  </ProtectedRoute>
                }
              >
                <Route index element={<>Home</>} />
                <Route path="dashboard" element={<>Dashboard</>} />
                <Route path="files" element={<FilesPage />} />
                <Route
                  path="options"
                  element={
                    <ProtectedRoute requiredRole="Admin">
                      <>Options</>
                    </ProtectedRoute>
                  }
                />
              </Route>

              <Route path="/" element={<Navigate to="/app" replace />} />
            </Routes>
          </Router>
        </ConfirmProvider>
      </AppThemeProvider>
    </Box>
  );
}

export default App;
