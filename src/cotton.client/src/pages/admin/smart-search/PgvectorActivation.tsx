import { Alert, Box, Button, Stack, Typography } from "@mui/material";
import { useState, type ReactNode } from "react";
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
  nextAction: ReactNode;
}

export const PgvectorActivation = ({
  databaseName,
  permissionDenied,
  error,
  pending,
  disabled,
  onEnable,
  onRefresh,
  nextAction,
}: PgvectorActivationProps) => {
  const { t } = useTranslation("admin");
  const [showManual, setShowManual] = useState(false);
  return (
    <Stack spacing={1.5}>
      {error && <Alert severity="error">{error}</Alert>}
      {!permissionDenied && (
        <Stack
          direction="row"
          sx={{
            flexWrap: "wrap",
            gap: 1,
          }}
        >
          <Stack direction="row" spacing={1}>
            <Button
              variant="contained"
              loading={pending}
              disabled={disabled}
              onClick={onEnable}
            >
              {t("smartSearch.actions.enable")}
            </Button>
            {nextAction}
          </Stack>
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
            sx={{
              alignItems: { xs: "stretch", sm: "center" },
              gap: 1.5,
            }}
          >
            <Box
              sx={{
                flex: 1,
                minWidth: 0,
              }}
            >
              <SetupCommand command={ENABLE_VECTOR_SQL} />
            </Box>
            <Stack direction="row" spacing={1} sx={{ flexShrink: 0 }}>
              <Button
                color="inherit"
                variant="outlined"
                disabled={disabled || pending}
                onClick={onRefresh}
              >
                {t("smartSearch.actions.checkAgain")}
              </Button>
              {permissionDenied && nextAction}
            </Stack>
          </Stack>
        </Stack>
      )}
    </Stack>
  );
};
