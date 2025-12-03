import React from "react";
import { useAuthStore } from "../store/authStore";
import LoginPage from "./LoginPage";
import type { ReactKitProps } from "../types";
import { AxiosProvider } from "../contexts/AxiosContext";

type Props = React.PropsWithChildren<{
  appProps: ReactKitProps;
}>;

const ProtectedContent: React.FC<Props> = ({ children, appProps }) => {
  const accessToken = useAuthStore((s) => s.accessToken);
  const refreshToken = useAuthStore((s) => s.refreshToken);
  const apiService = useAuthStore((s) => s.apiService);

  if (!accessToken && !refreshToken) {
    return appProps.renderLoginPage ? (
      <>{appProps.renderLoginPage(appProps)}</>
    ) : (
      <LoginPage appProps={appProps} />
    );
  }

  if (!apiService) {
    return null;
  }
  return <AxiosProvider>{children}</AxiosProvider>;
};

export default ProtectedContent;
