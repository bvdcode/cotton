import { AppRoutes } from "./app/routes";
import { AuthProvider } from "./features/auth";
import { BrowserRouter } from "react-router-dom";
import { ThemeContextProvider } from "./app/providers";
import { ConfirmProvider } from "material-ui-confirm";
import { QueryClientProvider } from "@tanstack/react-query";
import { ReactQueryDevtools } from "@tanstack/react-query-devtools";
import { queryClient } from "./shared/api/queries/queryClient";
import { AppBootstrap } from "./app/AppBootstrap";

function App() {
  return (
    <QueryClientProvider client={queryClient}>
      <ThemeContextProvider>
        <ConfirmProvider>
          <AuthProvider>
            <AppBootstrap />
            <BrowserRouter>
              <AppRoutes />
            </BrowserRouter>
          </AuthProvider>
        </ConfirmProvider>
      </ThemeContextProvider>
      {import.meta.env.DEV && <ReactQueryDevtools initialIsOpen={false} />}
    </QueryClientProvider>
  );
}

export default App;
