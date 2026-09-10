import {
  Avatar,
  Box,
  Container,
  Paper,
  Typography,
  type ContainerProps,
} from "@mui/material";
import type { ReactNode } from "react";

type AuthActionShellProps = {
  children: ReactNode;
  fullScreenOnMobile?: boolean;
  logoAlt: string;
  title: ReactNode;
  maxWidth?: ContainerProps["maxWidth"];
};

export const AuthActionShell = ({
  children,
  fullScreenOnMobile = false,
  logoAlt,
  title,
  maxWidth = "sm",
}: AuthActionShellProps) => (
  <Container
    maxWidth={maxWidth}
    sx={{
      height: fullScreenOnMobile ? { xs: "100%", sm: "auto" } : undefined,
      minHeight: "100%",
      display: "flex",
      alignItems: "center",
      justifyContent: "center",
      py: fullScreenOnMobile ? { xs: 0, sm: 4 } : 4,
      px: fullScreenOnMobile ? { xs: 0, sm: 3 } : undefined,
    }}
  >
    <Paper
      sx={{
        p: fullScreenOnMobile ? { xs: 2, sm: 4 } : 4,
        width: "100%",
        minHeight: fullScreenOnMobile ? { xs: "100%", sm: "auto" } : undefined,
        borderRadius: fullScreenOnMobile ? { xs: 0, sm: 1 } : undefined,
      }}
    >
      <Box
        display="flex"
        justifyContent="space-between"
        alignItems="center"
        gap={1.5}
      >
        <Typography variant="h4" component="h1" sx={{ flex: 1, minWidth: 0 }}>
          {title}
        </Typography>
        <Avatar
          src="/assets/icons/icon.svg"
          alt={logoAlt}
          sx={{ flexShrink: 0 }}
        />
      </Box>

      {children}
    </Paper>
  </Container>
);
