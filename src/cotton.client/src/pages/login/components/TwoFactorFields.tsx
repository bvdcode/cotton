import { Stack, Typography } from "@mui/material";
import { OneTimeCodeInput } from "@shared/ui/OneTimeCodeInput";

interface TwoFactorFieldsProps {
  caption: string;
  digitAriaLabel: string;
  value: string;
  onChange: (value: string) => void;
  disabled: boolean;
}

export const TwoFactorFields = ({
  caption,
  digitAriaLabel,
  value,
  onChange,
  disabled,
}: TwoFactorFieldsProps) => (
  <Stack spacing={2.5}>
    <Typography variant="body2" color="text.secondary" align="center">
      {caption}
    </Typography>
    <OneTimeCodeInput
      value={value}
      onChange={onChange}
      disabled={disabled}
      autoFocus={true}
      inputAriaLabel={digitAriaLabel}
    />
  </Stack>
);
