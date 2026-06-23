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
  logoAlt: string;
  title: ReactNode;
  maxWidth?: ContainerProps["maxWidth"];
};

export const AuthActionShell = ({
  children,
  logoAlt,
  title,
  maxWidth = "sm",
}: AuthActionShellProps) => (
  <Container
    maxWidth={maxWidth}
    sx={{
      minHeight: "100%",
      display: "flex",
      alignItems: "center",
      justifyContent: "center",
      py: 4,
    }}
  >
    <Paper sx={{ p: 4, width: "100%" }}>
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
