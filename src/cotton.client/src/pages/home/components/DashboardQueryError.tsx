import { Alert, Button } from "@mui/material";
import { useTranslation } from "react-i18next";

interface DashboardQueryErrorProps {
  message: string;
  onRetry: () => void;
}

export const DashboardQueryError = ({
  message,
  onRetry,
}: DashboardQueryErrorProps) => {
  const { t } = useTranslation("home");

  return (
    <Alert
      severity="error"
      action={
        <Button color="inherit" size="small" onClick={onRetry}>
          {t("dashboard.actions.retry")}
        </Button>
      }
    >
      {message}
    </Alert>
  );
};
