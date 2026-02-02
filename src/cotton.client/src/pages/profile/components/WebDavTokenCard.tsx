import {
  Box,
  Button,
  Stack,
  Typography,
  Alert,
  IconButton,
  Tooltip,
  Accordion,
  AccordionSummary,
  AccordionDetails,
  Divider,
} from "@mui/material";
import ContentCopyIcon from "@mui/icons-material/ContentCopy";
import { Folder, Key } from "@mui/icons-material";
import ExpandMoreIcon from "@mui/icons-material/ExpandMore";
import { useTranslation } from "react-i18next";
import { useState } from "react";
import { confirm } from "material-ui-confirm";
import { authApi } from "../../../shared/api/authApi";
import { useAuth } from "../../../features/auth";
import { ProfileAccordionCard } from "./ProfileAccordionCard";

type ReadonlyFieldProps = {
  label: string;
  value: string;
  tooltip?: string;
  onCopy?: (value: string) => void;
};

const ReadonlyField = ({
  label,
  value,
  tooltip,
  onCopy,
}: ReadonlyFieldProps) => {
  return (
    <Stack>
      <Typography variant="body2" color="text.secondary" sx={{ mb: 0.5 }}>
        {label}
      </Typography>
      <Stack direction="row" alignItems="center" spacing={1}>
        <Box sx={{ fontFamily: "monospace", wordBreak: "break-all" }}>
          {value}
        </Box>
        <Tooltip title={tooltip ?? label}>
          <IconButton size="small" onClick={() => onCopy?.(value)}>
            <ContentCopyIcon fontSize="small" />
          </IconButton>
        </Tooltip>
      </Stack>
    </Stack>
  );
};

export const WebDavTokenCard = () => {
  const { t } = useTranslation("profile");
  const { user } = useAuth();
  const [token, setToken] = useState<string | null>(null);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const endsWithSlash = window.location.origin.endsWith("/");
  const webDavUrl = `${window.location.origin}${endsWithSlash ? "" : "/"}api/v1/webdav/`;
  const username = user?.username ?? "";
  const tokenOrPlaceholder = token ?? t("webdav.placeholderPassword");

  const examples = [
    {
      id: "windows",
      command: `net use Z: "${webDavUrl}" /user:${username} ${tokenOrPlaceholder} /persistent:yes`,
    },
    {
      id: "windows-registry",
      command:
        'reg add "HKLM\\SYSTEM\\CurrentControlSet\\Services\\WebClient\\Parameters" /v FileSizeLimitInBytes /t REG_DWORD /d 4294967295 /f',
    },
    {
      id: "powershell",
      command: `cmd /c net use Z: "${webDavUrl}" /user:${username} "${tokenOrPlaceholder}" /persistent:yes`,
    },
    {
      id: "mac",
      command: `mount_webdav "${webDavUrl}" /Volumes/Cotton -i`,
    },
    {
      id: "linux",
      command: `mkdir -p /mnt/cotton && mount -t davfs "${webDavUrl}" /mnt/cotton`,
    },
  ];
  const handleGenerateToken = () => {
    confirm({
      title: t("webdav.confirmTitle"),
      description: t("webdav.confirmDescription"),
      confirmationText: t("webdav.confirmButton"),
      cancellationText: t("webdav.cancelButton"),
    }).then(async (result) => {
      if (result.confirmed) {
        try {
          setLoading(true);
          setError(null);
          const newToken = await authApi.getWebDavToken();
          setToken(newToken);
        } catch (err) {
          setError(
            err instanceof Error ? err.message : t("webdav.errors.failed"),
          );
        } finally {
          setLoading(false);
        }
      }
    });
  };

  const handleCopy = async (value: string) => {
    if (!value) return;
    await navigator.clipboard.writeText(value);
  };

  return (
    <ProfileAccordionCard
      id="webdav-header"
      ariaControls="webdav-content"
      icon={<Folder color="primary" />}
      title={t("webdav.title")}
      description={t("webdav.description")}
    >
      <Stack spacing={2}>
        <Stack spacing={1} sx={{ mt: 1 }}>
          <Stack
            direction={{ xs: "column", sm: "row" }}
            spacing={{ xs: 1.5, sm: 3 }}
            sx={{ justifyContent: "space-between" }}
          >
            <ReadonlyField
              label={t("webdav.usernameLabel")}
              value={username}
              tooltip={t("webdav.copyUsername")}
              onCopy={handleCopy}
            />

            <ReadonlyField
              label={t("webdav.connectUrlLabel")}
              value={webDavUrl}
              tooltip={t("webdav.copyUrl")}
              onCopy={handleCopy}
            />
          </Stack>
          {token && (
            <ReadonlyField
              label={t("webdav.tokenLabel")}
              value={token}
              tooltip={t("webdav.copyToken")}
              onCopy={handleCopy}
            />
          )}
          <Typography variant="caption" color="text.secondary">
            {t("webdav.cardHelp")}
          </Typography>
        </Stack>

        {error && (
          <Alert severity="error" onClose={() => setError(null)}>
            {error}
          </Alert>
        )}

        {token ? (
          <Stack spacing={1}>
            <Alert severity="warning">{t("webdav.tokenWarning")}</Alert>
          </Stack>
        ) : (
          <Button
            variant="contained"
            onClick={handleGenerateToken}
            disabled={loading}
            startIcon={<Key />}
          >
            {loading ? t("webdav.generating") : t("webdav.generateButton")}
          </Button>
        )}

        <Accordion
          disableGutters
          sx={{
            boxShadow: "none",
            "&:before": { display: "none" },
          }}
        >
          <AccordionSummary
            expandIcon={<ExpandMoreIcon />}
            sx={{
              px: 0,
              minHeight: 48,
              "& .MuiAccordionSummary-content": { margin: 0 },
              "& .MuiAccordionSummary-expandIconWrapper": {
                color: "text.secondary",
              },
            }}
          >
            <Typography variant="body2" fontWeight={500}>
              {t("webdav.examples.title")}
            </Typography>
          </AccordionSummary>

          <AccordionDetails sx={{ px: 0, pb: 0 }}>
            <Stack spacing={0} divider={<Divider />}>
              {examples.map((example) => (
                <Stack key={example.id} spacing={0.5} sx={{ py: 1.5 }}>
                  <Box display="flex" justifyContent="space-between">
                    <Typography variant="body2" fontWeight={600}>
                      {t(`webdav.examples.${example.id}.title`)}
                    </Typography>
                    <Typography variant="caption" color="text.secondary">
                      {t(`webdav.examples.${example.id}.caption`)}
                    </Typography>
                  </Box>
                  <Box
                    sx={{
                      p: 1.5,
                      bgcolor: "action.hover",
                      borderRadius: 1,
                      border: (theme) => `1px solid ${theme.palette.divider}`,
                      display: "flex",
                      alignItems: "center",
                      gap: 1,
                    }}
                  >
                    <Box
                      sx={{
                        flex: 1,
                        fontFamily: "monospace",
                        fontSize: "0.8rem",
                        wordBreak: "break-all",
                      }}
                    >
                      {example.command}
                    </Box>
                    <Tooltip title={t("webdav.examples.copy")}>
                      <IconButton
                        size="small"
                        onClick={() => handleCopy(example.command)}
                      >
                        <ContentCopyIcon fontSize="small" />
                      </IconButton>
                    </Tooltip>
                  </Box>
                </Stack>
              ))}
            </Stack>
          </AccordionDetails>
        </Accordion>
      </Stack>
    </ProfileAccordionCard>
  );
};
