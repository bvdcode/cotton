import NavTabs from "./NavTabs";
import UserMenu from "./UserMenu";
import Branding from "./Branding";
import { ReactKitProps } from "../types";
import { FunctionComponent } from "react";
import { Box, AppBar, Toolbar } from "@mui/material";
import { useLocation, useNavigate } from "react-router-dom";

const NavigationBar: FunctionComponent<ReactKitProps> = ({
  pages,
  logoUrl,
  appName,
}) => {
  const location = useLocation();
  const navigate = useNavigate();

  return (
    <AppBar position="static">
      <Toolbar
        sx={{
          gap: 2,
          userSelect: "none",
          display: "grid",
          gridTemplateColumns: "1fr auto 1fr",
        }}
      >
        <Branding
          appName={appName}
          logoUrl={logoUrl}
          onClick={() => navigate("/")}
        />
        <NavTabs
          pages={pages}
          currentPath={location.pathname}
          onNavigate={(route: string) => navigate(route)}
        />
        <UserMenu />
      </Toolbar>
    </AppBar>
  );
};

export default NavigationBar;
