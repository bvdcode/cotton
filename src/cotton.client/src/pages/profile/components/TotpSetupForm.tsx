import {
  Box,
  Button,
  CircularProgress,
  Divider,
  IconButton,
  Stack,
  Typography,
  useTheme,
} from "@mui/material";
import { useTranslation } from "react-i18next";
import QRCode from "react-qr-code";
import { OneTimeCodeInput } from "../../../shared/ui/OneTimeCodeInput";
import type { TotpSetup } from "../../../shared/api/totpApi";
import { ContentCopy } from "@mui/icons-material";

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
    <Box mt={3}>
      <Divider sx={{ mb: 3 }} />

      <Stack spacing={2.5}>
        <Stack
          direction={{ xs: "column", md: "row" }}
          spacing={{ xs: 3, md: 4 }}
          alignItems={{ xs: "stretch", md: "flex-start" }}
        >
          <Stack
            spacing={2}
            alignItems={{ xs: "center", md: "flex-start" }}
            flex={1}
          >
            <Box
              display="flex"
              justifyContent={{ xs: "center", md: "flex-start" }}
              width="100%"
            >
              <Box
                p={2}
                borderRadius={2}
                display="inline-flex"
                bgcolor={theme.palette.common.white}
                border="1px solid"
                borderColor="divider"
              >
                <QRCode
                  value={totpSetup.otpAuthUri}
                  size={200}
                  level="M"
                  fgColor={theme.palette.common.black}
                  bgColor={theme.palette.common.white}
                />
              </Box>
            </Box>

            <Typography
              variant="caption"
              color="text.secondary"
              sx={{
                display: "block",
                wordBreak: "break-all",
                fontFamily: "monospace",
                bgcolor: "action.hover",
                p: 1.5,
                borderRadius: 1,
                width: "100%",
              }}
            >
              {t("totp.setup.secretLabel")}:{" "}
              <Box component="strong" sx={{ color: "text.primary" }}>
                {totpSetup.secretBase32}
              </Box>
              <IconButton onClick={onCopySecret} size="small">
                <ContentCopy fontSize="small" />
              </IconButton>
            </Typography>
          </Stack>

          <Stack
            spacing={2}
            flex={1}
            alignItems={{ xs: "center", md: "flex-start" }}
          >
            <Box width="100%">
              <Typography variant="subtitle2" fontWeight={600} gutterBottom>
                {t("totp.confirm.title")}
              </Typography>
              <Typography variant="caption" color="text.secondary">
                {t("totp.confirm.caption")}
              </Typography>
            </Box>

            <Box
              width="100%"
              display="flex"
              justifyContent={{ xs: "center", md: "flex-start" }}
            >
              <OneTimeCodeInput
                value={totpCode}
                onChange={onTotpCodeChange}
                disabled={totpConfirmLoading}
                autoFocus={false}
                inputAriaLabel={t("totp.confirm.digit")}
              />
            </Box>

            <Box
              width="100%"
              display="flex"
              justifyContent={{ xs: "center", md: "flex-start" }}
            >
              <Button
                variant="contained"
                onClick={onConfirm}
                disabled={totpConfirmLoading}
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
          </Stack>
        </Stack>
      </Stack>
    </Box>
  );
};
