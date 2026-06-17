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
import { useTranslation } from "react-i18next";
import { useCallback, useEffect, useState, type ReactNode } from "react";
import { useParams } from "react-router-dom";
import {
  appCodeApi,
  type AppCodeDetails,
} from "../../shared/api/appCodeApi";
import { getApiErrorMessage } from "../../shared/api/httpClient";
import { AuthActionShell } from "../../shared/ui/AuthActionShell";
import { formatAppCodeOrigin } from "./appCodeOrigin";

type ViewState =
  | { kind: "loading" }
  | { kind: "ready"; details: AppCodeDetails }
  | { kind: "approved"; details: AppCodeDetails }
  | { kind: "denied"; details: AppCodeDetails }
  | { kind: "error"; message: string };

const ACCESS_BULLET_KEYS = ["files", "content", "profile", "session"] as const;

export const AppCodeApprovalPage = () => {
  const { t } = useTranslation("appCodeApproval");
  const { t: tCommon } = useTranslation("common");
  const { id } = useParams<{ id: string }>();
  const [state, setState] = useState<ViewState>({ kind: "loading" });
  const [submitting, setSubmitting] = useState(false);

  useEffect(() => {
    let cancelled = false;

    const loadDetails = async () => {
      if (!id) {
        setState({ kind: "error", message: t("errors.missingRequest") });
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
              t("errors.unavailable"),
          });
        }
      }
    };

    void loadDetails();

    return () => {
      cancelled = true;
    };
  }, [id, t]);

  const approve = useCallback(async () => {
    if (!id || state.kind !== "ready") return;
    setSubmitting(true);
    try {
      await appCodeApi.approve(id);
      setState({ kind: "approved", details: state.details });
    } catch (error) {
      setState({
        kind: "error",
        message: getApiErrorMessage(error) ?? t("errors.approveFailed"),
      });
    } finally {
      setSubmitting(false);
    }
  }, [id, state, t]);

  const deny = useCallback(async () => {
    if (!id || state.kind !== "ready") return;
    setSubmitting(true);
    try {
      await appCodeApi.deny(id);
      setState({ kind: "denied", details: state.details });
    } catch (error) {
      setState({
        kind: "error",
        message: getApiErrorMessage(error) ?? t("errors.denyFailed"),
      });
    } finally {
      setSubmitting(false);
    }
  }, [id, state, t]);

  return (
    <AuthActionShell
      title={t("title")}
      logoAlt={tCommon("app.logoAlt")}
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
          title={t("approved.title")}
          message={t("approved.message")}
        />
      )}
      {state.kind === "denied" && (
        <FinalState
          severity="info"
          icon={<Close color="info" />}
          title={t("denied.title")}
          message={t("denied.message")}
        />
      )}
    </AuthActionShell>
  );
};

const LoadingState = () => {
  const { t } = useTranslation("appCodeApproval");

  return (
    <Box display="flex" alignItems="center" gap={2} mt={3}>
      <CircularProgress size={24} />
      <Typography color="text.secondary">
        {t("loading")}
      </Typography>
    </Box>
  );
};

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
}: ApprovalStateProps) => {
  const { t } = useTranslation("appCodeApproval");
  const origin = formatAppCodeOrigin(
    details.origin,
    t("request.localOrigin"),
  );

  return (
    <Stack spacing={3} sx={{ mt: 3 }}>
      <Stack spacing={0.75}>
        <Box display="flex" alignItems="center" gap={1}>
          <LoginOutlined color="primary" />
          <Typography variant="h6" component="h2">
            {details.applicationName}
          </Typography>
        </Box>
        <Typography color="text.secondary">
          {details.deviceName
            ? t("request.versionWithDevice", {
                version: details.applicationVersion,
                deviceName: details.deviceName,
              })
            : t("request.version", { version: details.applicationVersion })}
        </Typography>
        <Typography color="text.secondary">
          {t("request.origin", { origin })}
        </Typography>
      </Stack>

      <Divider />

      <Box>
        <Box display="flex" alignItems="center" gap={1} mb={1}>
          <VerifiedUserOutlined color="primary" fontSize="small" />
          <Typography variant="subtitle1">
            {t("access.title")}
          </Typography>
        </Box>
        <List dense disablePadding>
          {ACCESS_BULLET_KEYS.map((key) => (
            <ListItem key={key} disableGutters>
              <ListItemIcon sx={{ minWidth: 30 }}>
                <RadioButtonChecked color="primary" sx={{ fontSize: 12 }} />
              </ListItemIcon>
              <ListItemText primary={t(`access.bullets.${key}`)} />
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
          {t("actions.allow")}
        </Button>
        <Button
          variant="outlined"
          color="inherit"
          startIcon={<Close />}
          onClick={onDeny}
          disabled={submitting}
          fullWidth
        >
          {t("actions.deny")}
        </Button>
      </Stack>
    </Stack>
  );
};

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
