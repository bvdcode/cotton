import {
  Alert,
  Box,
  Button,
  CircularProgress,
  Divider,
  List,
  ListItem,
  ListItemIcon,
  ListItemText,
  Stack,
  Typography,
} from "@mui/material";
import {
  CheckCircleOutline,
  Close,
  LoginOutlined,
  RadioButtonChecked,
  VerifiedUserOutlined,
} from "@mui/icons-material";
import { useCallback, useEffect, useState, type ReactNode } from "react";
import { useParams } from "react-router-dom";
import {
  appCodeApi,
  type AppCodeDetails,
} from "../../shared/api/appCodeApi";
import { getApiErrorMessage } from "../../shared/api/httpClient";
import { AuthActionShell } from "../../shared/ui/AuthActionShell";

type ViewState =
  | { kind: "loading" }
  | { kind: "ready"; details: AppCodeDetails }
  | { kind: "approved"; details: AppCodeDetails }
  | { kind: "denied"; details: AppCodeDetails }
  | { kind: "error"; message: string };

const ACCESS_BULLETS = [
  "Access your Cotton Cloud files and folders.",
  "Upload, download, rename, move, and delete content.",
  "Read basic account profile information.",
  "Stay signed in until you revoke the application session.",
];

export const AppCodeApprovalPage = () => {
  const { id } = useParams<{ id: string }>();
  const [state, setState] = useState<ViewState>({ kind: "loading" });
  const [submitting, setSubmitting] = useState(false);

  useEffect(() => {
    let cancelled = false;

    const loadDetails = async () => {
      if (!id) {
        setState({ kind: "error", message: "Sign-in request is missing." });
        return;
      }

      setState({ kind: "loading" });
      try {
        const details = await appCodeApi.getDetails(id);
        if (!cancelled) {
          setState(createLoadedState(details));
        }
      } catch (error) {
        if (!cancelled) {
          setState({
            kind: "error",
            message:
              getApiErrorMessage(error) ??
              "This sign-in request is no longer available.",
          });
        }
      }
    };

    void loadDetails();

    return () => {
      cancelled = true;
    };
  }, [id]);

  const approve = useCallback(async () => {
    if (!id || state.kind !== "ready") return;
    setSubmitting(true);
    try {
      await appCodeApi.approve(id);
      setState({ kind: "approved", details: state.details });
    } catch (error) {
      setState({
        kind: "error",
        message: getApiErrorMessage(error) ?? "Failed to approve sign-in.",
      });
    } finally {
      setSubmitting(false);
    }
  }, [id, state]);

  const deny = useCallback(async () => {
    if (!id || state.kind !== "ready") return;
    setSubmitting(true);
    try {
      await appCodeApi.deny(id);
      setState({ kind: "denied", details: state.details });
    } catch (error) {
      setState({
        kind: "error",
        message: getApiErrorMessage(error) ?? "Failed to deny sign-in.",
      });
    } finally {
      setSubmitting(false);
    }
  }, [id, state]);

  return (
    <AuthActionShell
      title="Application sign-in"
      logoAlt="Cotton Cloud"
      maxWidth="sm"
    >
      {state.kind === "loading" && <LoadingState />}
      {state.kind === "error" && <ErrorState message={state.message} />}
      {state.kind === "ready" && (
        <ApprovalState
          details={state.details}
          submitting={submitting}
          onApprove={approve}
          onDeny={deny}
        />
      )}
      {state.kind === "approved" && (
        <FinalState
          severity="success"
          icon={<CheckCircleOutline color="success" />}
          title="Access granted"
          message="You can return to the application."
        />
      )}
      {state.kind === "denied" && (
        <FinalState
          severity="info"
          icon={<Close color="info" />}
          title="Request denied"
          message="The application was not signed in."
        />
      )}
    </AuthActionShell>
  );
};

const LoadingState = () => (
  <Box display="flex" alignItems="center" gap={2} mt={3}>
    <CircularProgress size={24} />
    <Typography color="text.secondary">
      Please wait, loading sign-in details...
    </Typography>
  </Box>
);

const createLoadedState = (details: AppCodeDetails): ViewState => {
  if (details.status === "approved") {
    return { kind: "approved", details };
  }

  if (details.status === "denied") {
    return { kind: "denied", details };
  }

  return { kind: "ready", details };
};

type ErrorStateProps = {
  message: string;
};

const ErrorState = ({ message }: ErrorStateProps) => (
  <Alert severity="error" sx={{ mt: 3 }}>
    {message}
  </Alert>
);

type ApprovalStateProps = {
  details: AppCodeDetails;
  submitting: boolean;
  onApprove: () => void;
  onDeny: () => void;
};

const ApprovalState = ({
  details,
  submitting,
  onApprove,
  onDeny,
}: ApprovalStateProps) => (
  <Stack spacing={3} sx={{ mt: 3 }}>
    <Stack spacing={0.75}>
      <Box display="flex" alignItems="center" gap={1}>
        <LoginOutlined color="primary" />
        <Typography variant="h6" component="h2">
          {details.applicationName}
        </Typography>
      </Box>
      <Typography color="text.secondary">
        Version {details.applicationVersion}
        {details.deviceName ? ` on ${details.deviceName}` : ""}
      </Typography>
      <Typography color="text.secondary">
        Request from {details.origin}
      </Typography>
    </Stack>

    <Divider />

    <Box>
      <Box display="flex" alignItems="center" gap={1} mb={1}>
        <VerifiedUserOutlined color="primary" fontSize="small" />
        <Typography variant="subtitle1">
          This application will be able to:
        </Typography>
      </Box>
      <List dense disablePadding>
        {ACCESS_BULLETS.map((item) => (
          <ListItem key={item} disableGutters>
            <ListItemIcon sx={{ minWidth: 30 }}>
              <RadioButtonChecked color="primary" sx={{ fontSize: 12 }} />
            </ListItemIcon>
            <ListItemText primary={item} />
          </ListItem>
        ))}
      </List>
    </Box>

    <Stack direction={{ xs: "column", sm: "row" }} spacing={1.5}>
      <Button
        variant="contained"
        startIcon={<CheckCircleOutline />}
        onClick={onApprove}
        disabled={submitting}
        fullWidth
      >
        Allow
      </Button>
      <Button
        variant="outlined"
        color="inherit"
        startIcon={<Close />}
        onClick={onDeny}
        disabled={submitting}
        fullWidth
      >
        Deny
      </Button>
    </Stack>
  </Stack>
);

type FinalStateProps = {
  icon: ReactNode;
  message: string;
  severity: "success" | "info";
  title: string;
};

const FinalState = ({ icon, message, severity, title }: FinalStateProps) => (
  <Alert
    severity={severity}
    icon={icon}
    sx={{ mt: 3, alignItems: "center" }}
  >
    <Typography fontWeight={600}>{title}</Typography>
    <Typography variant="body2">{message}</Typography>
  </Alert>
);
