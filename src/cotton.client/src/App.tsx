import { AppRoutes } from "./app/routes";
import { AuthProvider } from "./features/auth";
import { BrowserRouter } from "react-router-dom";
import { ThemeContextProvider } from "./app/providers";
import { ConfirmProvider } from "material-ui-confirm";

function App() {
  return (
    <ThemeContextProvider>
      <ConfirmProvider>
        <AuthProvider>
          <BrowserRouter>
            <AppRoutes />
          </BrowserRouter>
        </AuthProvider>
      </ConfirmProvider>
    </ThemeContextProvider>
  );
}

export default App;
