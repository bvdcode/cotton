import { AppRoutes } from "./app/routes";
import { AuthProvider } from "./features/auth";
import { PhotoProvider } from "react-photo-view";
import { BrowserRouter } from "react-router-dom";
import { ThemeContextProvider } from "./app/providers";
import { ConfirmProvider } from "material-ui-confirm";
import "react-photo-view/dist/react-photo-view.css";

function App() {
  return (
    <ThemeContextProvider>
      <ConfirmProvider>
        <PhotoProvider>
          <AuthProvider>
            <BrowserRouter>
              <AppRoutes />
            </BrowserRouter>
          </AuthProvider>
        </PhotoProvider>
      </ConfirmProvider>
    </ThemeContextProvider>
  );
}

export default App;
