import {
  FormControl,
  MenuItem,
  Select,
  Stack,
  Tooltip,
  Typography,
} from "@mui/material";
import { useCallback, useState, type SyntheticEvent } from "react";
import type { GeoIpLookupMode } from "../../../shared/api/settingsApi";
import { geoIpOptions } from "./adminGeneralSettingsModel";

type AdminGeoIpLookupModeFieldProps = {
  value: GeoIpLookupMode;
  loading: boolean;
  disabled: boolean;
  telemetryEnabled: boolean;
  label: string;
  getLabel: (mode: GeoIpLookupMode) => string;
  getDescription: (mode: GeoIpLookupMode) => string;
  onChange: (mode: GeoIpLookupMode) => void;
};

export const AdminGeoIpLookupModeField = ({
  value,
  loading,
  disabled,
  telemetryEnabled,
  label,
  getLabel,
  getDescription,
  onChange,
}: AdminGeoIpLookupModeFieldProps) => {
  const [menuWidth, setMenuWidth] = useState<number | undefined>(undefined);

  const handleOpen = useCallback((event: SyntheticEvent) => {
    const target = event.currentTarget as HTMLElement;
    const width = Math.round(target.getBoundingClientRect().width);
    if (width > 0) {
      setMenuWidth(width);
    }
  }, []);

  return (
    <Stack spacing={1}>
      <Stack
        direction={{ xs: "column", sm: "row" }}
        spacing={1}
        alignItems={{ xs: "flex-start", sm: "baseline" }}
        justifyContent="space-between"
        sx={{ minHeight: 24 }}
      >
        <Typography variant="subtitle2" fontWeight={700}>
          {label}
        </Typography>
        <Tooltip title={getDescription(value)} placement="top">
          <Typography
            variant="caption"
            color="text.secondary"
            sx={{
              textAlign: { xs: "left", sm: "right" },
              maxWidth: { xs: "100%", sm: "66%" },
              overflow: "hidden",
              textOverflow: "ellipsis",
              whiteSpace: { xs: "normal", sm: "nowrap" },
            }}
          >
            {getDescription(value)}
          </Typography>
        </Tooltip>
      </Stack>

      <FormControl fullWidth>
        <Select
          value={value}
          onOpen={handleOpen}
          onChange={(event) => onChange(event.target.value as GeoIpLookupMode)}
          disabled={disabled || loading}
          renderValue={(selected) => getLabel(selected as GeoIpLookupMode)}
          MenuProps={{
            PaperProps: {
              sx: {
                width: menuWidth ? `${menuWidth}px` : undefined,
                maxWidth: "100%",
              },
            },
          }}
        >
          {geoIpOptions.map((option) => (
            <MenuItem
              key={option}
              value={option}
              disabled={!telemetryEnabled && option === "CottonCloud"}
              sx={{ alignItems: "flex-start" }}
            >
              <Stack spacing={0.25} py={0.25}>
                <Typography variant="body2">{getLabel(option)}</Typography>
                <Typography
                  variant="caption"
                  color="text.secondary"
                  sx={{ lineHeight: 1.35, whiteSpace: "normal" }}
                >
                  {getDescription(option)}
                </Typography>
              </Stack>
            </MenuItem>
          ))}
        </Select>
      </FormControl>
    </Stack>
  );
};
