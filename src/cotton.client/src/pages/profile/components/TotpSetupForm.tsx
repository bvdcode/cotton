import {
  Box,
  Button,
  CircularProgress,
  Divider,
  Typography,
  useTheme,
} from "@mui/material";
import { useTranslation } from "react-i18next";
import QRCode from "react-qr-code";
import { OneTimeCodeInput } from "../../../shared/ui/OneTimeCodeInput";
import type { TotpSetup } from "../../../shared/api/totpApi";

interface TotpSetupFormProps {
  totpSetup: TotpSetup;
  totpCode: string;
  totpConfirmLoading: boolean;
  onTotpCodeChange: (code: string) => void;
  onConfirm: () => void;
  onCopySecret: () => void;
}

export const TotpSetupForm = ({
  totpSetup,
  totpCode,
  totpConfirmLoading,
  onTotpCodeChange,
  onConfirm,
  onCopySecret,
}: TotpSetupFormProps) => {
  const { t } = useTranslation("profile");
  const theme = useTheme();

  return (
    <Box sx={{ mt: 3 }}>
      <Divider sx={{ mb: 3 }} />

      <Typography variant="subtitle2" fontWeight={600} gutterBottom>
        {t("totp.setup.qrTitle")}
      </Typography>

      <Box
        sx={{
          mt: 2,
          mb: 3,
          p: 2,
          borderRadius: 2,
          display: "inline-flex",
          bgcolor: theme.palette.mode === "dark" ? "background.paper" : "#fff",
          border: (theme) => `1px solid ${theme.palette.divider}`,
        }}
      >
        <QRCode
          value={totpSetup.otpAuthUri}
          size={200}
          level="M"
          fgColor={theme.palette.text.primary}
          bgColor={
            theme.palette.mode === "dark"
              ? theme.palette.background.paper
              : "#ffffff"
          }
        />
      </Box>

      <Box sx={{ mb: 2 }}>
        <Button variant="outlined" onClick={onCopySecret} size="small">
          {t("totp.setup.copySecret")}
        </Button>
      </Box>

      <Typography
        variant="caption"
        color="text.secondary"
        sx={{
          display: "block",
          mb: 3,
          wordBreak: "break-all",
          fontFamily: "monospace",
          bgcolor: "action.hover",
          p: 1.5,
          borderRadius: 1,
        }}
      >
        {t("totp.setup.secretLabel")}:{" "}
        <Box component="strong" sx={{ color: "text.primary" }}>
          {totpSetup.secretBase32}
        </Box>
      </Typography>

      <Divider sx={{ mb: 3 }} />

      <Typography variant="subtitle2" fontWeight={600} gutterBottom>
        {t("totp.confirm.title")}
      </Typography>
      <Typography
        variant="caption"
        color="text.secondary"
        sx={{ display: "block", mb: 2 }}
      >
        {t("totp.confirm.caption")}
      </Typography>

      <Box sx={{ mb: 3 }}>
        <OneTimeCodeInput
          value={totpCode}
          onChange={onTotpCodeChange}
          disabled={totpConfirmLoading}
          autoFocus={false}
          inputAriaLabel={t("totp.confirm.digit")}
        />
      </Box>

      <Button
        variant="contained"
        onClick={onConfirm}
        disabled={totpConfirmLoading}
        fullWidth={false}
      >
        {totpConfirmLoading ? (
          <>
            <CircularProgress size={16} sx={{ mr: 1 }} />
            {t("totp.confirm.loading")}
          </>
        ) : (
          t("totp.confirm.button")
        )}
      </Button>
    </Box>
  );
};
