import { Alert, Box, Button, Stack, Typography } from "@mui/material";
import { useState } from "react";
import { useTranslation } from "react-i18next";
import { ENABLE_VECTOR_SQL } from "./smartSearchSetup";
import { SetupCommand } from "./SetupCommand";

interface PgvectorActivationProps {
  databaseName: string;
  permissionDenied: boolean;
  error: string | null;
  pending: boolean;
  disabled: boolean;
  onEnable: () => void;
  onRefresh: () => void;
}

export const PgvectorActivation = ({
  databaseName,
  permissionDenied,
  error,
  pending,
  disabled,
  onEnable,
  onRefresh,
}: PgvectorActivationProps) => {
  const { t } = useTranslation("admin");
  const [showManual, setShowManual] = useState(false);
  return (
    <Stack spacing={1.5}>
      {error && <Alert severity="error">{error}</Alert>}
      {!permissionDenied && (
        <Stack direction="row" flexWrap="wrap" gap={1}>
          <Button
            variant="contained"
            loading={pending}
            disabled={disabled}
            onClick={onEnable}
          >
            {t("smartSearch.actions.enable")}
          </Button>
          <Button
            color="inherit"
            aria-expanded={showManual}
            onClick={() => setShowManual(!showManual)}
          >
            {t("smartSearch.activation.manual")}
          </Button>
        </Stack>
      )}
      {(permissionDenied || showManual) && (
        <Stack spacing={1}>
          <Typography
            variant="body2"
            role={permissionDenied ? "alert" : undefined}
          >
            {permissionDenied && (
              <>
                <span>{t("smartSearch.activation.permissionDenied")}</span>{" "}
              </>
            )}
            {t("smartSearch.activation.sqlInstruction", {
              database: databaseName,
            })}
          </Typography>
          <Stack
            direction={{ xs: "column", sm: "row" }}
            alignItems={{ xs: "stretch", sm: "center" }}
            gap={1.5}
          >
            <Box flex={1} minWidth={0}>
              <SetupCommand command={ENABLE_VECTOR_SQL} />
            </Box>
            <Button
              color="inherit"
              variant="outlined"
              disabled={disabled || pending}
              onClick={onRefresh}
              sx={{
                alignSelf: { xs: "flex-start", sm: "center" },
                flexShrink: 0,
              }}
            >
              {t("smartSearch.actions.checkAgain")}
            </Button>
          </Stack>
        </Stack>
      )}
    </Stack>
  );
};
