import { AppRoutes } from "./app/routes";
import { AuthProvider } from "./features/auth";
import { BrowserRouter } from "react-router-dom";
import { ThemeContextProvider } from "./app/providers";
import { ConfirmProvider, type ConfirmOptions } from "material-ui-confirm";
import { QueryClientProvider } from "@tanstack/react-query";
import { queryClient } from "./shared/api/queries/queryClient";
import { AppBootstrap } from "./app/AppBootstrap";
import { NotificationProvider } from "@shared/ui/notifications";
import { safeConfirmFocusOptions } from "@shared/ui/confirmOptions";

const confirmProviderOptions: ConfirmOptions = {
  ...safeConfirmFocusOptions,
  dialogProps: {
    sx: { zIndex: 10000 },
  },
};

function App() {
  return (
    <QueryClientProvider client={queryClient}>
      <ThemeContextProvider>
        <ConfirmProvider defaultOptions={confirmProviderOptions}>
          <NotificationProvider>
            <AuthProvider>
              <AppBootstrap />
              <BrowserRouter>
                <AppRoutes />
              </BrowserRouter>
            </AuthProvider>
          </NotificationProvider>
        </ConfirmProvider>
      </ThemeContextProvider>
    </QueryClientProvider>
  );
}

export default App;
