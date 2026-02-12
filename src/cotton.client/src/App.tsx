import { AppRoutes } from "./app/routes";
import { AuthProvider } from "./features/auth";
import { BrowserRouter } from "react-router-dom";
import { ThemeContextProvider } from "./app/providers";
import { ConfirmProvider } from "material-ui-confirm";
import { useEventHub } from "./features/notifications";
import { AppErrorBoundary } from "./app/components/AppErrorBoundary";

const EventHubBootstrap = () => {
  useEventHub();
  return null;
};

function App() {
  return (
    <AppErrorBoundary>
      <ThemeContextProvider>
        <ConfirmProvider>
          <AuthProvider>
            <EventHubBootstrap />
            <BrowserRouter>
              <AppRoutes />
            </BrowserRouter>
          </AuthProvider>
        </ConfirmProvider>
      </ThemeContextProvider>
    </AppErrorBoundary>
  );
}

export default App;
