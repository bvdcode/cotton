import { Box, Button, Paper, Stack, Typography, Alert } from "@mui/material";
import { Key } from "@mui/icons-material";
import { useTranslation } from "react-i18next";
import { useState } from "react";
import { confirm } from "material-ui-confirm";
import { filesApi } from "../../../shared/api/filesApi";

export const WebDavTokenCard = () => {
  const { t } = useTranslation("profile");
  const [token, setToken] = useState<string | null>(null);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);

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
          const newToken = await filesApi.getWebDavToken();
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

  const handleCopyToken = async () => {
    if (token) {
      await navigator.clipboard.writeText(token);
    }
  };

  return (
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
            <Button variant="outlined" size="small" onClick={handleCopyToken}>
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
  );
};
