import React, { type ReactNode } from "react";
import {
  Box,
  Button,
  Collapse,
  Divider,
  Typography,
} from "@mui/material";
import { useTranslation } from "react-i18next";

interface BoundaryProps {
  title: string;
  description: string;
  reloadLabel: string;
  detailsLabel: string;
  hideDetailsLabel: string;
  children: ReactNode;
}

interface BoundaryState {
  hasError: boolean;
  showDetails: boolean;
  errorMessage: string | null;
  errorStack: string | null;
}

class AppErrorBoundaryBase extends React.Component<BoundaryProps, BoundaryState> {
  state: BoundaryState = {
    hasError: false,
    showDetails: false,
    errorMessage: null,
    errorStack: null,
  };

  static getDerivedStateFromError(): BoundaryState {
    return {
      hasError: true,
      showDetails: false,
      errorMessage: null,
      errorStack: null,
    };
  }

  componentDidCatch(error: Error): void {
    console.error("App crashed with an unhandled error", error);
    this.setState({
      errorMessage: error.message || "Error",
      errorStack: error.stack ?? null,
    });
  }

  private handleReload = () => {
    window.location.reload();
  };

  private handleToggleDetails = () => {
    this.setState((s) => ({ showDetails: !s.showDetails }));
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

          <Box display="flex" gap={1} mt={1}>
            <Button variant="contained" onClick={this.handleReload}>
              {this.props.reloadLabel}
            </Button>
            <Button variant="text" onClick={this.handleToggleDetails}>
              {this.state.showDetails
                ? this.props.hideDetailsLabel
                : this.props.detailsLabel}
            </Button>
          </Box>

          <Collapse in={this.state.showDetails}>
            <Box display="flex" flexDirection="column" gap={1} mt={1}>
              <Divider />
              <Typography variant="caption" color="text.secondary">
                {this.state.errorMessage}
              </Typography>
              {this.state.errorStack ? (
                <Typography
                  variant="caption"
                  color="text.secondary"
                  sx={{ whiteSpace: "pre-wrap" }}
                >
                  {this.state.errorStack}
                </Typography>
              ) : null}
            </Box>
          </Collapse>
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
      detailsLabel={t("errors.showDetails")}
      hideDetailsLabel={t("errors.hideDetails")}
    >
      {children}
    </AppErrorBoundaryBase>
  );
};
