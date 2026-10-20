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
import { ContentCopy } from "@mui/icons-material";
import { useMemo } from "react";
import { useTranslation } from "react-i18next";
import qrcode from "qrcode-generator";
import { OneTimeCodeInput } from "../../../shared/ui/OneTimeCodeInput";
import type { TotpSetup } from "../../../shared/api/totpApi";

const QR_CODE_CELL_SIZE = 8;
const QR_CODE_MARGIN = QR_CODE_CELL_SIZE * 4;

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
  const qrCodeImageUrl: string = useMemo(() => {
    const qrCode: ReturnType<typeof qrcode> = qrcode(0, "M");
    qrCode.addData(totpSetup.otpAuthUri);
    qrCode.make();

    return qrCode.createDataURL(QR_CODE_CELL_SIZE, QR_CODE_MARGIN);
  }, [totpSetup.otpAuthUri]);

  return (
    <Box
      sx={{
        mt: 3,
      }}
    >
      <Divider sx={{ mb: 3 }} />

      <Stack spacing={2.5}>
        <Stack
          direction={{ xs: "column", md: "row" }}
          spacing={{ xs: 3, md: 4 }}
          sx={{
            alignItems: { xs: "stretch", md: "flex-start" },
          }}
        >
          <Stack
            spacing={2}
            sx={{
              alignItems: { xs: "center", md: "flex-start" },
              flex: 1,
            }}
          >
            <Box
              sx={{
                display: "flex",
                justifyContent: { xs: "center", md: "flex-start" },
                width: "100%",
              }}
            >
              <Box
                sx={{
                  width: "100%",
                  p: 2,
                  borderRadius: 2,
                  display: "inline-flex",
                  bgcolor: theme.palette.common.white,
                  border: "1px solid",
                  borderColor: "divider",
                  justifyContent: "center",
                }}
              >
                <Box
                  component="img"
                  src={qrCodeImageUrl}
                  alt={t("totp.setup.qrTitle")}
                  sx={{
                    width: "100%",
                    height: "auto",
                    display: "block",
                    imageRendering: "pixelated",
                  }}
                />
              </Box>
            </Box>

            <Box
              sx={{
                width: "100%",
                display: "flex",
                flexDirection: "row",
                alignItems: "center",
                justifyContent: "space-between",
                gap: 1,
                border: "1px solid",
                borderColor: "divider",
                borderRadius: 1,
                bgcolor: "background.default",
                p: 1,
              }}
            >
              <Typography
                component="code"
                variant="body2"
                sx={{ fontFamily: "monospace", wordBreak: "break-all" }}
              >
                {totpSetup.secretBase32}
              </Typography>
              <IconButton
                onClick={onCopySecret}
                size="small"
                aria-label={t("totp.setup.copySecret")}
              >
                <ContentCopy fontSize="small" />
              </IconButton>
            </Box>
          </Stack>

          <Stack
            spacing={2}
            sx={{
              flex: 1,
              alignItems: { xs: "center", md: "flex-start" },
            }}
          >
            <Box
              sx={{
                width: "100%",
              }}
            >
              <Typography
                variant="subtitle2"
                gutterBottom
                sx={{
                  fontWeight: 600,
                }}
              >
                {t("totp.confirm.title")}
              </Typography>
              <Typography
                variant="caption"
                sx={{
                  color: "text.secondary",
                }}
              >
                {t("totp.confirm.caption")}
              </Typography>
            </Box>

            <Box
              sx={{
                width: "100%",
                display: "flex",
                justifyContent: { xs: "center", md: "flex-start" },
              }}
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
              sx={{
                width: "100%",
                display: "flex",
                justifyContent: { xs: "center", md: "flex-start" },
              }}
            >
              <Button
                fullWidth
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
