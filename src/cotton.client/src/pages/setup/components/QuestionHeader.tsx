import { Stack, Typography, Button } from "@mui/material";
import OpenInNewIcon from "@mui/icons-material/OpenInNew";
import { type ReactNode } from "react";

export function QuestionHeader({
  title,
  subtitle,
  linkUrl,
  linkAriaLabel,
  extraHeader,
}: {
  title: string;
  subtitle: string;
  linkUrl?: string;
  linkAriaLabel?: string;
  extraHeader?: ReactNode;
}) {
  return (
    <Stack
      spacing={0.4}
      direction="row"
      alignItems="center"
      justifyContent="space-between"
    >
      <Stack spacing={0.4}>
        <Stack direction="row" spacing={0.5} alignItems="center">
          <Typography variant="h6" fontWeight={700}>
            {title}
          </Typography>
          {extraHeader}
        </Stack>
        <Typography variant="body2" color="text.secondary">
          {subtitle}
        </Typography>
      </Stack>
      {linkUrl ? (
        <Button
          href={linkUrl}
          target="_blank"
          rel="noreferrer"
          variant="text"
          size="small"
          aria-label={linkAriaLabel}
          sx={{
            minWidth: 0,
            p: 0.75,
            borderRadius: 1.5,
          }}
        >
          <OpenInNewIcon fontSize="small" />
        </Button>
      ) : null}
    </Stack>
  );
}
