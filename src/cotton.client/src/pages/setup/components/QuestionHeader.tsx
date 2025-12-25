import { Stack, Typography, Button } from "@mui/material";
import OpenInNewIcon from "@mui/icons-material/OpenInNew";

export function QuestionHeader({
  title,
  subtitle,
  linkUrl,
  linkAriaLabel,
}: {
  title: string;
  subtitle: string;
  linkUrl?: string;
  linkAriaLabel?: string;
}) {
  return (
    <Stack
      spacing={0.4}
      direction="row"
      alignItems="center"
      justifyContent="space-between"
    >
      <Stack spacing={0.4}>
        <Typography variant="h6" fontWeight={700}>
          {title}
        </Typography>
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
