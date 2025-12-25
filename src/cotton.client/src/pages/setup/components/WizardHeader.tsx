import { Stack, Typography } from "@mui/material";

export function WizardHeader({ t }: { t: (key: string) => string }) {
  return (
    <Stack spacing={1.5}>
      <Stack spacing={0.5}>
        <Typography variant="h4" fontWeight={800}>
          {t("title")}
        </Typography>
        <Typography variant="body1" color="text.secondary">
          {t("subtitle")}
        </Typography>
      </Stack>
    </Stack>
  );
}
