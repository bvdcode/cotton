import React from "react";
import { Box } from "@mui/material";
import "react-toastify/dist/ReactToastify.css";
import { ToastContainer } from "react-toastify";
import { ConfirmProvider } from "material-ui-confirm";
import { Login, NotFound, Dashboard } from "./components";
import { useThemeModeContext } from "./providers/ThemeContext";
import { Route, Routes, BrowserRouter as Router } from "react-router-dom";

const protectedRoutes: { path: string; element: React.ReactNode }[] = [
  { path: "/", element: <Dashboard /> },
  { path: "/dashboard", element: <Dashboard /> },
];

function App() {
  const { resolvedMode } = useThemeModeContext();

  return (
    <Box sx={{ position: "fixed", inset: 0 }}>
      <ConfirmProvider>
        <ToastContainer
          theme={(resolvedMode as "light" | "dark" | "colored") ?? "light"}
        />
        <Router>
          <Routes>
            <Route path="/login" element={<Login />} />
            <Route>
              {protectedRoutes.map(({ path, element }) => (
                <Route key={path} path={path} element={element} />
              ))}
            </Route>
            <Route path="*" element={<NotFound />} />
          </Routes>
        </Router>
      </ConfirmProvider>
    </Box>
  );
}

export default App;
