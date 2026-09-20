import CheckIcon from "@mui/icons-material/Check";
import ContentCopyIcon from "@mui/icons-material/ContentCopy";
import { Box, IconButton, Stack, Tooltip } from "@mui/material";
import { useTranslation } from "react-i18next";
import { useCopyFeedback } from "@shared/hooks/useCopyFeedback";
import { toast } from "@shared/ui/notifications";

export const SetupCommand = ({ command }: { command: string }) => {
  const { t } = useTranslation("admin");
  const [copied, markCopied] = useCopyFeedback();
  const copy = async () => {
    try {
      await navigator.clipboard.writeText(command);
      markCopied();
    } catch {
      toast.error(t("smartSearch.errors.copyFailed"));
    }
  };
  const copyLabel = t(
    copied ? "smartSearch.actions.copied" : "smartSearch.actions.copy",
  );

  return (
    <Stack
      direction="row"
      alignItems="center"
      spacing={1}
      px={1.5}
      py={1}
      sx={{ bgcolor: "action.hover", borderRadius: 1 }}
    >
      <Box
        component="pre"
        m={0}
        flex={1}
        minWidth={0}
        sx={{
          whiteSpace: "pre-wrap",
          overflowWrap: "anywhere",
          fontSize: "0.875rem",
        }}
      >
        <code>{command}</code>
      </Box>
      <Tooltip title={copyLabel}>
        <IconButton
          size="small"
          aria-label={copyLabel}
          onClick={() => void copy()}
        >
          {copied ? (
            <CheckIcon fontSize="small" />
          ) : (
            <ContentCopyIcon fontSize="small" />
          )}
        </IconButton>
      </Tooltip>
    </Stack>
  );
};
