import {
  Box,
  Button,
  Paper,
  Stack,
  Typography,
  Alert,
  Dialog,
  DialogTitle,
  DialogContent,
  DialogActions,
  IconButton,
  Tooltip,
} from "@mui/material";
import ContentCopyIcon from "@mui/icons-material/ContentCopy";
import { Key } from "@mui/icons-material";
import { useTranslation } from "react-i18next";
import { useState } from "react";
import { confirm } from "material-ui-confirm";
import { authApi } from "../../../shared/api/authApi";
import { useAuth } from "../../../features/auth";

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
  const [dialogOpen, setDialogOpen] = useState(false);

  const webDavUrl = `${window.location.origin}/api/v1/webdav/`;
  const username = user?.username ?? "";
  // Example commands (use generated token as password when available)
  const windowsCommand = `net use Z: "${webDavUrl}/" /user:${username} ${token ?? t("webdav.placeholderPassword")} /persistent:yes`;
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
          setDialogOpen(true);
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
    <>
      <Paper sx={{ p: { xs: 2, sm: 3 } }}>
        <Stack spacing={2}>
          <Stack direction="row" spacing={1} alignItems="center">
            <Key color="primary" />
            <Typography variant="h6" fontWeight={600}>
              {t("webdav.title")}
            </Typography>
          </Stack>

          <Typography variant="body2" color="text.secondary">
            {t("webdav.description")}
          </Typography>

          {/* Visible connection info — available without generating a token */}
          <Stack spacing={1} sx={{ mt: 1 }}>
            <Box display="flex" justifyContent="space-between">
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
            </Box>
            <Typography variant="caption" color="text.secondary">
              {t(
                "webdav.cardHelp",
                "Use these credentials to connect via a WebDAV client; generate a token to get a password shown once.",
              )}
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
              <Box
                sx={{
                  p: 2,
                  bgcolor: "action.hover",
                  borderRadius: 1,
                  fontFamily: "monospace",
                  fontSize: "0.875rem",
                  wordBreak: "break-all",
                  userSelect: "all",
                }}
              >
                {token}
              </Box>
              <Button
                variant="outlined"
                size="small"
                onClick={() => handleCopy(token)}
              >
                {t("webdav.copyToken")}
              </Button>
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
        </Stack>
      </Paper>

      <Dialog
        open={dialogOpen}
        onClose={() => setDialogOpen(false)}
        fullWidth
        maxWidth="sm"
        aria-labelledby="webdav-token-dialog"
      >
        <DialogTitle id="webdav-token-dialog">
          {t("webdav.modalTitle")}
        </DialogTitle>
        <DialogContent dividers>
          <Stack spacing={2} sx={{ mt: 1 }}>
            <Stack>
              <Typography
                variant="body2"
                color="text.secondary"
                sx={{ mb: 0.5 }}
              >
                {t("webdav.usernameLabel")}
              </Typography>
              <Stack direction="row" alignItems="center" spacing={1}>
                <Box sx={{ fontFamily: "monospace" }}>{username}</Box>
                <Tooltip title={t("webdav.copyUsername") as string}>
                  <IconButton size="small" onClick={() => handleCopy(username)}>
                    <ContentCopyIcon fontSize="small" />
                  </IconButton>
                </Tooltip>
              </Stack>
            </Stack>

            <Stack>
              <Typography
                variant="body2"
                color="text.secondary"
                sx={{ mb: 0.5 }}
              >
                {t("webdav.connectUrlLabel")}
              </Typography>
              <Stack direction="row" alignItems="center" spacing={1}>
                <Box sx={{ fontFamily: "monospace", wordBreak: "break-all" }}>
                  {webDavUrl}
                </Box>
                <Tooltip title={t("webdav.copyUrl") as string}>
                  <IconButton
                    size="small"
                    onClick={() => handleCopy(webDavUrl)}
                  >
                    <ContentCopyIcon fontSize="small" />
                  </IconButton>
                </Tooltip>
              </Stack>
            </Stack>

            <Stack spacing={1}>
              <Stack>
                <Typography
                  variant="body2"
                  color="text.secondary"
                  sx={{ mb: 0.5 }}
                >
                  {t("webdav.tokenLabel")}
                </Typography>
                <Stack direction="row" alignItems="center" spacing={1}>
                  <Box sx={{ fontFamily: "monospace", wordBreak: "break-all" }}>
                    {token}
                  </Box>
                  <Tooltip title={t("webdav.copyToken") as string}>
                    <IconButton
                      size="small"
                      onClick={() => handleCopy(token ?? "")}
                    >
                      <ContentCopyIcon fontSize="small" />
                    </IconButton>
                  </Tooltip>
                </Stack>
              </Stack>

              {/* Examples: Windows / PowerShell / macOS / Linux */}
              <Box>
                <Typography variant="subtitle2" sx={{ mb: 1 }}>
                  {t("webdav.examples.title")}
                </Typography>

                <Box
                  sx={{
                    display: "grid",
                    gridTemplateColumns: { xs: "1fr", md: "1fr 1fr" },
                    gap: 2,
                  }}
                >
                  {/* Windows (cmd) */}
                  <Box
                    sx={{
                      display: "flex",
                      flexDirection: "column",
                      gap: 1,
                      bgcolor: "background.default",
                      p: 1,
                      borderRadius: 1,
                      border: (theme) => `1px solid ${theme.palette.divider}`,
                    }}
                  >
                    <Typography variant="body2" sx={{ fontWeight: 600 }}>
                      {t("webdav.examples.windows.title")}
                    </Typography>
                    <Box sx={{ fontFamily: "monospace", wordBreak: "break-all", p: 1 }} data-testid="webdav-windows-cmd">
                      {windowsCommand}
                    </Box>
                    <Box sx={{ display: "flex", gap: 1, alignItems: "center" }}>
                      <Button size="small" variant="outlined" onClick={() => handleCopy(windowsCommand)} startIcon={<ContentCopyIcon fontSize="small" />}>
                        {t("webdav.examples.windows.copy")}
                      </Button>
                      <Typography variant="caption" color="text.secondary">
                        {t("webdav.examples.windows.caption")}
                      </Typography>
                    </Box>
                  </Box>

                  {/* PowerShell (escaped) */}
                  <Box
                    sx={{
                      display: "flex",
                      flexDirection: "column",
                      gap: 1,
                      bgcolor: "background.default",
                      p: 1,
                      borderRadius: 1,
                      border: (theme) => `1px solid ${theme.palette.divider}`,
                    }}
                  >
                    <Typography variant="body2" sx={{ fontWeight: 600 }}>
                      {t("webdav.examples.powershell.title")}
                    </Typography>
                    <Box sx={{ fontFamily: "monospace", wordBreak: "break-all", p: 1 }} data-testid="webdav-powershell-cmd">
                      {`cmd /c net use Z: "${webDavUrl}/" /user:${username} "${token ?? t('webdav.placeholderPassword')}" /persistent:yes`}
                    </Box>
                    <Box sx={{ display: "flex", gap: 1, alignItems: "center" }}>
                      <Button size="small" variant="outlined" onClick={() => handleCopy(`cmd /c net use Z: "${webDavUrl}/" /user:${username} "${token ?? t('webdav.placeholderPassword')}" /persistent:yes`)} startIcon={<ContentCopyIcon fontSize="small" />}>
                        {t("webdav.examples.powershell.copy")}
                      </Button>
                      <Typography variant="caption" color="text.secondary">
                        {t("webdav.examples.powershell.caption")}
                      </Typography>
                    </Box>
                  </Box>

                  {/* macOS */}
                  <Box
                    sx={{
                      display: "flex",
                      flexDirection: "column",
                      gap: 1,
                      bgcolor: "background.default",
                      p: 1,
                      borderRadius: 1,
                      border: (theme) => `1px solid ${theme.palette.divider}`,
                    }}
                  >
                    <Typography variant="body2" sx={{ fontWeight: 600 }}>
                      {t("webdav.examples.mac.title")}
                    </Typography>
                    <Box sx={{ fontFamily: "monospace", wordBreak: "break-all", p: 1 }} data-testid="webdav-mac-cmd">
                      {`mount_webdav "${webDavUrl}/" /Volumes/Cotton -i`}
                    </Box>
                    <Box sx={{ display: "flex", gap: 1, alignItems: "center" }}>
                      <Button size="small" variant="outlined" onClick={() => handleCopy(`mount_webdav "${webDavUrl}/" /Volumes/Cotton -i`)} startIcon={<ContentCopyIcon fontSize="small" />}>
                        {t("webdav.examples.mac.copy")}
                      </Button>
                      <Typography variant="caption" color="text.secondary">
                        {t("webdav.examples.mac.caption")}
                      </Typography>
                    </Box>
                  </Box>

                  {/* Linux (davfs2 / cadaver) */}
                  <Box
                    sx={{
                      display: "flex",
                      flexDirection: "column",
                      gap: 1,
                      bgcolor: "background.default",
                      p: 1,
                      borderRadius: 1,
                      border: (theme) => `1px solid ${theme.palette.divider}`,
                    }}
                  >
                    <Typography variant="body2" sx={{ fontWeight: 600 }}>
                      {t("webdav.examples.linux.title")}
                    </Typography>
                    <Box sx={{ fontFamily: "monospace", wordBreak: "break-all", p: 1 }} data-testid="webdav-linux-cmd">
                      {`# davfs2\nmkdir -p /mnt/cotton && sudo mount -t davfs "${webDavUrl}/" /mnt/cotton`}
                    </Box>
                    <Box sx={{ display: "flex", gap: 1, alignItems: "center" }}>
                      <Button size="small" variant="outlined" onClick={() => handleCopy(`mkdir -p /mnt/cotton && sudo mount -t davfs "${webDavUrl}/" /mnt/cotton`)} startIcon={<ContentCopyIcon fontSize="small" />}>
                        {t("webdav.examples.linux.copy")}
                      </Button>
                      <Typography variant="caption" color="text.secondary">
                        {t("webdav.examples.linux.caption")}
                      </Typography>
                    </Box>
                  </Box>
                </Box>
              </Box>

              <Alert severity="warning">{t("webdav.tokenWarning")}</Alert>
            </Stack>
          </Stack>
        </DialogContent>
        <DialogActions>
          <Button onClick={() => setDialogOpen(false)}>
            {t("webdav.closeButton")}
          </Button>
        </DialogActions>
      </Dialog>
    </>
  );
};
