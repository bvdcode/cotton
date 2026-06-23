import { Component, type ErrorInfo, type ReactNode } from "react";
import { Alert, AlertTitle, Box, Button, Collapse, Stack } from "@mui/material";
import { withTranslation, type WithTranslation } from "react-i18next";

interface OwnProps {
  children: ReactNode;
}

interface State {
  error: Error | null;
  detailsVisible: boolean;
}

type Props = OwnProps & WithTranslation;

class ErrorBoundaryImpl extends Component<Props, State> {
  state: State = { error: null, detailsVisible: false };

  static getDerivedStateFromError(error: Error): Partial<State> {
    return { error };
  }

  componentDidCatch(error: Error, info: ErrorInfo): void {
    console.error("Unhandled UI error", error, info);
  }

  handleReload = (): void => {
    window.location.reload();
  };

  toggleDetails = (): void => {
    this.setState((state) => ({
      detailsVisible: !state.detailsVisible,
    }));
  };

  render(): ReactNode {
    const { children, t } = this.props;
    const { detailsVisible, error } = this.state;

    if (!error) {
      return children;
    }

    return (
      <Box p={2} display="flex" justifyContent="center">
        <Alert
          severity="error"
          sx={{ maxWidth: 720, width: "100%" }}
          action={
            <Button color="inherit" size="small" onClick={this.handleReload}>
              {t("common:actions.reload")}
            </Button>
          }
        >
          <AlertTitle>{t("common:errors.unexpectedTitle")}</AlertTitle>
          <Stack spacing={1}>
            <Box>{t("common:errors.unexpectedDescription")}</Box>
            <Button
              size="small"
              variant="text"
              color="inherit"
              onClick={this.toggleDetails}
              sx={{ alignSelf: "flex-start", px: 0 }}
            >
              {detailsVisible
                ? t("common:errors.hideDetails")
                : t("common:errors.showDetails")}
            </Button>
            <Collapse in={detailsVisible}>
              <Box
                component="pre"
                sx={{
                  m: 0,
                  p: 1,
                  fontFamily: "monospace",
                  fontSize: 12,
                  overflowX: "auto",
                  whiteSpace: "pre-wrap",
                  wordBreak: "break-word",
                  bgcolor: "action.hover",
                  borderRadius: 1,
                }}
              >
                {error.stack ?? error.message}
              </Box>
            </Collapse>
          </Stack>
        </Alert>
      </Box>
    );
  }
}

export const ErrorBoundary = withTranslation(["common"])(ErrorBoundaryImpl);
