import {
  Box,
  Button,
  Card,
  CardContent,
  Stack,
  Typography,
  alpha,
} from "@mui/material";
import { useCallback, useMemo, useState, type ReactNode } from "react";
import { useTranslation } from "react-i18next";
import { useNavigate } from "react-router-dom";
import { setupStepDefinitions } from "./setupQuestions.tsx";
import {
  WizardHeader,
  WizardProgressBar,
  QuestionBlock,
  QuestionBlockMulti,
  FloatingBlobs,
} from "./components";

export function SetupWizardPage() {
  const { t } = useTranslation("setup");
  const navigate = useNavigate();
  const [multiuserChoiceKey, setMultiuserChoiceKey] = useState<string | null>(
    null,
  );
  const [unsafeMultiuserInteraction, setUnsafeMultiuserInteraction] = useState<
    boolean | null
  >(null);
  const [intendedUse, setIntendedUse] = useState<string[]>([]);
  const [allowTelemetry, setAllowTelemetry] = useState<boolean | null>(null);
  const [started, setStarted] = useState(false);
  const [stepIndex, setStepIndex] = useState(0);

  const toggleIntendedUse = useCallback((key: string) => {
    setIntendedUse((prev) =>
      prev.includes(key) ? prev.filter((k) => k !== key) : [...prev, key],
    );
  }, []);

  type BuiltStep = {
    key: string;
    render: () => ReactNode;
    isValid: () => boolean;
  };

  const buildSteps = useCallback((): BuiltStep[] => {
    return setupStepDefinitions.map((def) => {
      if (def.type === "single" && def.key === "multiuser") {
        const options = def.options.map((opt) => ({
          key: opt.key,
          label: opt.label(),
          description: opt.description?.(),
          value: opt.value,
          icon: opt.icon,
        }));

        return {
          key: def.key,
          render: () => (
            <QuestionBlock
              title={def.title()}
              subtitle={def.subtitle()}
              linkUrl={def.linkUrl}
              linkAriaLabel={def.linkAria?.()}
              options={options}
              selectedKey={multiuserChoiceKey}
              onSelect={(optKey, value) => {
                setMultiuserChoiceKey(optKey);
                setUnsafeMultiuserInteraction(value);
              }}
            />
          ),
          isValid: (): boolean => multiuserChoiceKey !== null,
        };
      }

      if (def.type === "multi") {
        const options = def.options.map((opt) => ({
          key: opt.key,
          label: opt.label(),
          icon: opt.icon,
        }));

        return {
          key: def.key,
          render: () => (
            <QuestionBlockMulti
              title={def.title()}
              subtitle={def.subtitle()}
              options={options}
              selectedKeys={intendedUse}
              onToggle={toggleIntendedUse}
            />
          ),
          isValid: (): boolean => intendedUse.length > 0,
        };
      }

      if (def.type === "single" && def.key === "telemetry") {
        const options = def.options.map((opt) => ({
          key: opt.key,
          label: opt.label(),
          description: opt.description?.(),
          value: opt.value,
          icon: opt.icon,
        }));

        return {
          key: def.key,
          render: () => (
            <QuestionBlock
              title={def.title()}
              subtitle={def.subtitle()}
              options={options}
              selectedValue={allowTelemetry}
              onSelect={(_, value) => setAllowTelemetry(value)}
            />
          ),
          isValid: (): boolean => allowTelemetry !== null,
        };
      }

      return {
        key: def.key,
        render: () => null,
        isValid: (): boolean => true,
      };
    });
  }, [multiuserChoiceKey, intendedUse, allowTelemetry, toggleIntendedUse]);

  const steps = useMemo(() => buildSteps(), [buildSteps]);

  const currentStep = steps[stepIndex];
  const isLastStep = stepIndex === steps.length - 1;
  const canProceed = currentStep?.isValid?.() ?? false;

  const handleStart = () => {
    setStarted(true);
    setStepIndex(0);
  };

  const handleNext = () => {
    if (!started) {
      handleStart();
      return;
    }
    if (isLastStep) {
      const submission = {
        unsafeMultiuserInteraction,
        intendedUse,
        allowTelemetry,
      };
      // Placeholder submit; replace with API call/save later.
      void submission;
      navigate("/");
      return;
    }
    setStepIndex((i) => Math.min(i + 1, steps.length - 1));
  };

  const handleBack = () => {
    if (!started || stepIndex === 0) {
      setStarted(false);
      setStepIndex(0);
      return;
    }
    setStepIndex((i) => Math.max(i - 1, 0));
  };

  return (
    <Box
      sx={{
        position: "relative",
        width: "100%",
        minHeight: "100vh",
        display: "flex",
        alignItems: "center",
        justifyContent: "center",
        p: { xs: 2, sm: 4 },
        overflow: "hidden",
        bgcolor: "background.default",
        "@keyframes floatA": {
          "0%": { transform: "translate3d(0,0,0)" },
          "50%": { transform: "translate3d(20px, -30px, 0)" },
          "100%": { transform: "translate3d(0,0,0)" },
        },
        "@keyframes floatB": {
          "0%": { transform: "translate3d(0,0,0)" },
          "50%": { transform: "translate3d(-25px, 25px, 0)" },
          "100%": { transform: "translate3d(0,0,0)" },
        },
        "@keyframes floatC": {
          "0%": { transform: "translate3d(0,0,0)" },
          "50%": { transform: "translate3d(30px, 20px, 0)" },
          "100%": { transform: "translate3d(0,0,0)" },
        },
      }}
    >
      <FloatingBlobs />

      <Card
        elevation={10}
        sx={{
          position: "relative",
          width: "100%",
          maxWidth: 920,
          minHeight: 600,
          borderRadius: 3,
          backdropFilter: "blur(10px)",
          bgcolor: "background.paper",
          borderColor: (theme) =>
            theme.palette.mode === "dark"
              ? alpha(theme.palette.primary.main, 0.15)
              : "divider",
          borderWidth: 1,
          borderStyle: "solid",
          boxShadow: (theme) =>
            theme.palette.mode === "dark"
              ? `0 30px 90px ${alpha(theme.palette.primary.main, 0.25)}, 0 10px 40px ${alpha(theme.palette.common.black, 0.5)}`
              : `0 30px 60px ${alpha(theme.palette.common.black, 0.12)}, 0 10px 30px ${alpha(theme.palette.common.black, 0.08)}`,
          zIndex: 1,
        }}
      >
        <CardContent
          sx={{
            p: { xs: 3, sm: 4 },
            display: "flex",
            flexDirection: "column",
            minHeight: "calc(600px - 48px)",
          }}
        >
          <WizardHeader t={t} />

          <Box sx={{ flex: 1, mt: 3.5, mb: 3.5, overflow: "auto" }}>
            {started ? (
              <Stack spacing={2.5}>
                <WizardProgressBar step={stepIndex + 1} total={steps.length} />
                {currentStep?.render()}
              </Stack>
            ) : (
              <Typography variant="body1" color="text.secondary">
                {t("intro")}
              </Typography>
            )}
          </Box>

          <Stack direction={{ xs: "column", sm: "row" }} spacing={2}>
            {started ? (
              <>
                <Button
                  variant="outlined"
                  color="inherit"
                  size="large"
                  fullWidth
                  onClick={handleBack}
                  disabled={stepIndex === 0}
                  sx={{
                    py: 1.3,
                    fontWeight: 700,
                    textTransform: "none",
                  }}
                >
                  {t("actions.back")}
                </Button>
                <Button
                  variant="contained"
                  color="primary"
                  size="large"
                  fullWidth
                  onClick={handleNext}
                  disabled={!canProceed}
                  sx={{
                    py: 1.3,
                    fontWeight: 700,
                    textTransform: "none",
                  }}
                >
                  {isLastStep ? t("actions.finish") : t("actions.next")}
                </Button>
              </>
            ) : (
              <Button
                variant="contained"
                color="primary"
                size="large"
                fullWidth
                onClick={handleStart}
                sx={{
                  py: 1.3,
                  fontWeight: 700,
                  textTransform: "none",
                }}
              >
                {t("actions.start")}
              </Button>
            )}
          </Stack>
        </CardContent>
      </Card>
    </Box>
  );
}
