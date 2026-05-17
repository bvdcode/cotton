import { Stack, TextField } from "@mui/material";

interface CredentialsFieldsProps {
  username: string;
  password: string;
  onUsernameChange: (value: string) => void;
  onUsernameBlur: () => void;
  onPasswordChange: (value: string) => void;
  disabled: boolean;
  usernameLabel: string;
  passwordLabel: string;
  usernameHasError: boolean;
}

export const CredentialsFields = ({
  username,
  password,
  onUsernameChange,
  onUsernameBlur,
  onPasswordChange,
  disabled,
  usernameLabel,
  passwordLabel,
  usernameHasError,
}: CredentialsFieldsProps) => (
  <Stack spacing={2}>
    <TextField
      fullWidth
      label={usernameLabel}
      margin="none"
      variant="outlined"
      value={username}
      onChange={(e) => onUsernameChange(e.target.value)}
      onBlur={onUsernameBlur}
      disabled={disabled}
      error={usernameHasError}
    />
    <TextField
      fullWidth
      label={passwordLabel}
      type="password"
      margin="none"
      variant="outlined"
      value={password}
      onChange={(e) => onPasswordChange(e.target.value)}
      disabled={disabled}
    />
  </Stack>
);
