import { Component, type ErrorInfo, type ReactNode } from "react";
import {
  Box,
  Button,
  Card,
  CardContent,
  Stack,
  Typography,
} from "@mui/material";
import { withTranslation, type WithTranslation } from "react-i18next";

interface OwnProps {
  children: ReactNode;
  resetKey: string;
}

interface State {
  error: Error | null;
}

type Props = OwnProps & WithTranslation;

class ErrorBoundaryImpl extends Component<Props, State> {
  state: State = { error: null };

  static getDerivedStateFromError(error: Error): Partial<State> {
    return { error };
  }

  componentDidCatch(error: Error, info: ErrorInfo): void {
    console.error("Unhandled UI error", error, info);
  }

  componentDidUpdate(previousProps: Props): void {
    if (this.state.error && previousProps.resetKey !== this.props.resetKey) {
      this.setState({ error: null });
    }
  }

  handleRetry = (): void => {
    this.setState({ error: null });
  };

  handleReload = (): void => {
    window.location.reload();
  };

  render(): ReactNode {
    const { children, t } = this.props;
    const { error } = this.state;

    if (!error) {
      return children;
    }

    return (
      <Box px={1} py={{ xs: 2, sm: 4 }} display="flex" justifyContent="center">
        <Card
          role="alert"
          variant="outlined"
          sx={{ maxWidth: 480, width: "100%" }}
        >
          <CardContent>
            <Stack spacing={2}>
              <Box>
                <Typography component="h2" variant="h6" gutterBottom>
                  {t("common:errors.unexpectedTitle")}
                </Typography>
                <Typography variant="body2" color="text.secondary">
                  {t("common:errors.unexpectedDescription")}
                </Typography>
              </Box>
              <Stack direction={{ xs: "column", sm: "row" }} spacing={1}>
                <Button variant="contained" onClick={this.handleRetry}>
                  {t("common:actions.retry")}
                </Button>
                <Button variant="outlined" onClick={this.handleReload}>
                  {t("common:actions.reload")}
                </Button>
              </Stack>
            </Stack>
          </CardContent>
        </Card>
      </Box>
    );
  }
}

export const ErrorBoundary = withTranslation(["common"])(ErrorBoundaryImpl);
