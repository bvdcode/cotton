import React, { type ReactNode } from "react";
import { Box, Button, Typography } from "@mui/material";
import { useTranslation } from "react-i18next";

interface BoundaryProps {
  title: string;
  description: string;
  reloadLabel: string;
  children: ReactNode;
}

interface BoundaryState {
  hasError: boolean;
}

class AppErrorBoundaryBase extends React.Component<BoundaryProps, BoundaryState> {
  state: BoundaryState = { hasError: false };

  static getDerivedStateFromError(): BoundaryState {
    return { hasError: true };
  }

  componentDidCatch(error: Error): void {
    console.error("App crashed with an unhandled error", error);
  }

  private handleReload = () => {
    window.location.reload();
  };

  render() {
    if (!this.state.hasError) {
      return this.props.children;
    }

    return (
      <Box
        display="flex"
        alignItems="center"
        justifyContent="center"
        height="100%"
        width="100%"
      >
        <Box display="flex" flexDirection="column" gap={1} maxWidth={480} px={2}>
          <Typography variant="h6">{this.props.title}</Typography>
          <Typography variant="body2" color="text.secondary">
            {this.props.description}
          </Typography>
          <Box display="flex" mt={1}>
            <Button variant="contained" onClick={this.handleReload}>
              {this.props.reloadLabel}
            </Button>
          </Box>
        </Box>
      </Box>
    );
  }
}

export const AppErrorBoundary = ({ children }: { children: ReactNode }) => {
  const { t } = useTranslation("common");
  return (
    <AppErrorBoundaryBase
      title={t("errors.unexpectedTitle")}
      description={t("errors.unexpectedDescription")}
      reloadLabel={t("actions.reload")}
    >
      {children}
    </AppErrorBoundaryBase>
  );
};
