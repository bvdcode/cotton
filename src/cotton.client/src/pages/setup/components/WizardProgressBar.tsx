import { Box, Stack, Typography, useTheme, alpha } from "@mui/material";

export function WizardProgressBar({
  step,
  total,
}: {
  step: number;
  total: number;
}) {
  const progress = Math.round((step / total) * 100);
  const theme = useTheme();
  return (
    <Stack spacing={0.5}>
      <Typography variant="body2" color="text.secondary">
        {progress}% · {step}/{total}
      </Typography>
      <Box
        sx={{
          width: "100%",
          height: 8,
          borderRadius: 999,
          bgcolor: alpha(
            theme.palette.text.primary,
            theme.palette.mode === "dark" ? 0.08 : 0.08
          ),
          overflow: "hidden",
        }}
      >
        <Box
          sx={{
            height: "100%",
            width: `${progress}%`,
            background: `linear-gradient(90deg, ${theme.palette.primary.main}, ${theme.palette.secondary.main})`,
            transition: "width 0.25s ease",
          }}
        />
      </Box>
    </Stack>
  );
}
