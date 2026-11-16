import RefreshIcon from "@mui/icons-material/Refresh";
import {
  Alert,
  Button,
  IconButton,
  Skeleton,
  Stack,
  Step,
  StepButton,
  StepContent,
  Stepper,
  Tooltip,
  Typography,
} from "@mui/material";
import { useState } from "react";
import { useTranslation } from "react-i18next";
import { AdminPageSurface } from "../components/AdminPageSurface";
import { ComputationModeSetting } from "../settings/ComputationModeSetting";
import { SmartSearchDatabaseSetup } from "./SmartSearchDatabaseSetup";
import { SmartSearchIndexing } from "./SmartSearchIndexing";
import { useSmartSearchSetup } from "./useSmartSearchSetup";

export const AdminSmartSearchPage = () => {
  const { t } = useTranslation("admin");
  const setup = useSmartSearchSetup();
  const {
    status,
    statusQuery,
    indexing,
    computation,
    refresh,
    busy,
    loadError,
  } = setup;
  const [selectedStep, setSelectedStep] = useState<number | null>(null);
  const databaseReady =
    Boolean(status?.extensionEnabled && status.indexReady) &&
    !statusQuery.isError;
  const computationReady =
    computation.data?.isReady === true && !computation.isError;
  const firstIncomplete = !databaseReady ? 0 : !computationReady ? 1 : 2;
  const activeStep = Math.min(selectedStep ?? firstIncomplete, firstIncomplete);
  const steps = [
    { title: "database", ready: databaseReady, available: true },
    { title: "service", ready: computationReady, available: databaseReady },
    {
      title: "search",
      ready: indexing.savedValue && databaseReady && computationReady,
      available: databaseReady && computationReady,
    },
  ];
  const loading = statusQuery.isPending || indexing.status === "loading";

  return (
    <AdminPageSurface>
      <Stack spacing={2} sx={{ p: { xs: 2, sm: 3 } }}>
        <Stack
          direction="row"
          spacing={1}
          sx={{ justifyContent: "space-between", alignItems: "center" }}
        >
          <Typography component="h1" variant="h5" sx={{ fontWeight: 700 }}>
            {t("smartSearch.title")}
          </Typography>
          <Tooltip title={t("smartSearch.actions.refresh")}>
            <span>
              <IconButton
                aria-label={t("smartSearch.actions.refresh")}
                disabled={busy}
                onClick={() => void refresh()}
              >
                <RefreshIcon />
              </IconButton>
            </span>
          </Tooltip>
        </Stack>
        {loading ? (
          <Skeleton
            variant="rounded"
            height={160}
            role="status"
            aria-label={t("smartSearch.loading")}
          />
        ) : (
          <Stepper activeStep={activeStep} orientation="vertical" nonLinear>
            {steps.map((step, index) => (
              <Step key={step.title} completed={step.ready && step.available}>
                <StepButton
                  disabled={!step.available}
                  onClick={() => setSelectedStep(index)}
                  optional={
                    <Typography variant="caption" color="text.secondary">
                      {t(
                        `smartSearch.wizard.${step.ready && step.available ? "complete" : "step"}`,
                        { current: index + 1, total: steps.length },
                      )}
                    </Typography>
                  }
                >
                  <Typography sx={{ fontWeight: 600 }}>
                    {t(`smartSearch.wizard.${step.title}`)}
                  </Typography>
                </StepButton>
                <StepContent>
                  <Stack spacing={2} sx={{ pt: 1, pb: 1, minWidth: 0 }}>
                    {index === 0 && (
                      <>
                        {status && (
                          <Typography variant="caption" color="text.secondary">
                            {t("smartSearch.database", {
                              version: status.postgresMajorVersion,
                              database: status.databaseName,
                            })}
                          </Typography>
                        )}
                        {statusQuery.isError && (
                          <Alert severity="error">{loadError}</Alert>
                        )}
                        <SmartSearchDatabaseSetup
                          setup={setup}
                          nextAction={
                            <Button
                              variant="contained"
                              disabled={!databaseReady}
                              onClick={() => setSelectedStep(1)}
                            >
                              {t("smartSearch.wizard.next")}
                            </Button>
                          }
                        />
                      </>
                    )}
                    {index === 1 && (
                      <>
                        <Typography variant="body2" color="text.secondary">
                          {t("smartSearch.wizard.serviceHelp")}
                        </Typography>
                        <ComputationModeSetting smartSearch />
                        <Button
                          variant="contained"
                          disabled={!computationReady}
                          onClick={() => setSelectedStep(2)}
                          sx={{ alignSelf: "flex-start" }}
                        >
                          {t("smartSearch.wizard.next")}
                        </Button>
                      </>
                    )}
                    {index === 2 && <SmartSearchIndexing setup={setup} />}
                  </Stack>
                </StepContent>
              </Step>
            ))}
          </Stepper>
        )}
      </Stack>
    </AdminPageSurface>
  );
};
