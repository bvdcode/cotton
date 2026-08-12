import type { ReactNode } from "react";
import { ToggleButtonGroup } from "@mui/material";

interface SettingsToggleButtonGroupProps<TValue extends string | number> {
  ariaLabel: string;
  children: ReactNode;
  disabled: boolean;
  onChange: (value: TValue | null) => void;
  value: TValue;
}

export const SettingsToggleButtonGroup = <
  TValue extends string | number,
>({
  ariaLabel,
  children,
  disabled,
  onChange,
  value,
}: SettingsToggleButtonGroupProps<TValue>) => (
  <ToggleButtonGroup
    size="small"
    exclusive
    value={value}
    onChange={(_, next: TValue | null) => onChange(next)}
    disabled={disabled}
    aria-label={ariaLabel}
    fullWidth
    sx={{
      "& .MuiToggleButton-root": {
        flex: 1,
        minWidth: 0,
        whiteSpace: "normal",
        lineHeight: 1.2,
      },
    }}
  >
    {children}
  </ToggleButtonGroup>
);
