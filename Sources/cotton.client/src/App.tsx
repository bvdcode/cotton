import {
  Routes,
  Route,
  Navigate,
  BrowserRouter as Router,
} from "react-router-dom";
import { Box } from "@mui/material";
import "react-toastify/dist/ReactToastify.css";
import { ConfirmProvider } from "material-ui-confirm";
import { AppLayout, ProtectedRoute } from "./components";
import AppThemeProvider from "./providers/ThemeProvider";

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
                <Route path="leadstream" element={<>Lead Stream</>} />
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
