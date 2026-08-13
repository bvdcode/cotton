import { Alert, AlertTitle } from "@mui/material";

interface LoginInfoAlertProps {
  title: string;
  message: string;
}

export const LoginInfoAlert = ({ title, message }: LoginInfoAlertProps) => (
  <Alert severity="info">
    <AlertTitle>{title}</AlertTitle>
    {message}
  </Alert>
);
