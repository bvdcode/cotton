import { Box, Chip, Paper, Stack, Typography } from "@mui/material";
import type { AlertColor } from "@mui/material";
import { alpha } from "@mui/material/styles";
import CheckCircleIcon from "@mui/icons-material/CheckCircle";
import type { TFunction } from "i18next";
import type { ReactNode } from "react";
import type { SecurityDiagnosticsDto } from "../../../shared/api/adminApi";

export interface DiagnosticsContentSectionProps {
  diagnostics: SecurityDiagnosticsDto;
  t: TFunction<"admin">;
}

interface DiagnosticsRowProps {
  label: string;
  value: string;
  color?: AlertColor | "default";
}

export const DiagnosticsRow = ({
  label,
  value,
  color = "default",
}: DiagnosticsRowProps) => (
  <Box
    sx={{
      display: "grid",
      gridTemplateColumns: { xs: "1fr", sm: "220px minmax(0, 1fr)" },
      gap: { xs: 0.5, sm: 2 },
      alignItems: "center",
    }}
  >
    <Typography variant="body2" color="text.secondary">
      {label}
    </Typography>
    <Box>
      <Chip size="small" color={color} variant="outlined" label={value} />
    </Box>
  </Box>
);

interface DiagnosticsSectionProps {
  title: string;
  children: ReactNode;
}

export const DiagnosticsSection = ({
  title,
  children,
}: DiagnosticsSectionProps) => (
  <Stack spacing={1.5}>
    <Typography variant="subtitle1" fontWeight={700}>
      {title}
    </Typography>
    <Stack spacing={1}>{children}</Stack>
  </Stack>
);

interface RiskLabeledBlockProps {
  label: string;
  text: string;
}

export const RiskLabeledBlock = ({ label, text }: RiskLabeledBlockProps) => (
  <Box>
    <Typography
      variant="caption"
      color="text.secondary"
      fontWeight={700}
      sx={{ display: "block", textTransform: "uppercase", letterSpacing: 0.4 }}
    >
      {label}
    </Typography>
    <Typography variant="body2">{text}</Typography>
  </Box>
);

interface PositiveCardProps {
  title: string;
  code?: string;
  children?: ReactNode;
}

export const PositiveCard = ({ title, code, children }: PositiveCardProps) => (
  <Paper
    variant="outlined"
    sx={(theme) => ({
      overflow: "hidden",
      display: "flex",
      flexDirection: "column",
      borderLeft: `4px solid ${theme.palette.success.main}`,
    })}
  >
    <Box
      sx={(theme) => ({
        display: "flex",
        alignItems: "center",
        gap: 1,
        px: 2,
        py: 1,
        color: theme.palette.success.main,
        bgcolor: alpha(theme.palette.success.main, 0.1),
      })}
    >
      <CheckCircleIcon fontSize="small" />
      <Typography variant="subtitle2" fontWeight={700} color="inherit">
        {title}
      </Typography>
      {code && (
        <Chip
          size="small"
          variant="outlined"
          label={code}
          sx={{ ml: "auto" }}
        />
      )}
    </Box>
    {children && (
      <Stack spacing={1.25} sx={{ px: 2, py: 1.5 }}>
        {children}
      </Stack>
    )}
  </Paper>
);
