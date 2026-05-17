import { Alert, AlertTitle } from "@mui/material";

interface FirstRunAlertProps {
  title: string;
  message: string;
}

export const FirstRunAlert = ({ title, message }: FirstRunAlertProps) => (
  <Alert severity="info">
    <AlertTitle>{title}</AlertTitle>
    {message}
  </Alert>
);
