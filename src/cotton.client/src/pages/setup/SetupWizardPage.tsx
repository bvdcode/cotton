import {
  Box,
  Button,
  Card,
  CardContent,
  Stack,
  Fade,
  alpha,
  Alert,
  Typography,
  Link,
  CircularProgress,
} from "@mui/material";
import { useCallback, useState } from "react";
import { useTranslation } from "react-i18next";
import { useNavigate } from "react-router-dom";
import { WizardHeader, WizardProgressBar, FloatingBlobs } from "./components";
import { useSetupSteps } from "./useSetupSteps.tsx";
import { useAuth } from "../../features/auth/useAuth";
import { UserRole } from "../../features/auth/types";
import { settingsApi } from "../../shared/api/settingsApi";
import { setupStepDefinitions } from "./setupQuestions.tsx";

// Helper function to convert keys to values for server
function convertAnswersToValues(answers: Record<string, unknown>): Record<string, unknown> {
  const converted: Record<string, unknown> = {};
  
  for (const [questionKey, answer] of Object.entries(answers)) {
    const stepDef = setupStepDefinitions.find(s => s.key === questionKey);
    
    if (!stepDef) {
      // Keep as-is if not found (form fields, etc)
      converted[questionKey] = answer;
      continue;
    }
    
    if (stepDef.type === "single" && typeof answer === "string") {
      // Find the option and get its value
      const options = "getOptions" in stepDef && stepDef.getOptions 
        ? stepDef.getOptions() 
        : stepDef.options;
      const option = options.find(opt => opt.key === answer);
      converted[questionKey] = option?.value ?? answer;
    } else {
      // Keep as-is for multi, form types
      converted[questionKey] = answer;
    }
  }
  
  return converted;
}

export function SetupWizardPage() {
  const { t } = useTranslation("setup");
  const navigate = useNavigate();
  const { user } = useAuth();
  const [started, setStarted] = useState(false);
  const [stepIndex, setStepIndex] = useState(0);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);

  // Generic answers storage
  const [answers, setAnswers] = useState<Record<string, unknown>>({});

  const updateAnswer = useCallback((key: string, value: unknown) => {
    setAnswers((prev) => ({ ...prev, [key]: value }));
  }, []);

  const updateFormField = useCallback(
    (stepKey: string, fieldKey: string, value: string | boolean) => {
      setAnswers((prev) => ({
        ...prev,
        [stepKey]: {
          ...(prev[stepKey] || {}),
          [fieldKey]: value,
        },
      }));
    },
    [],
  );

  const steps = useSetupSteps(answers, updateAnswer, updateFormField);

  const currentStep = steps[stepIndex];
  const isLastStep = stepIndex === steps.length - 1;
  const canProceed = currentStep?.isValid?.() ?? false;

  const handleStart = () => {
    setStarted(true);
    setStepIndex(0);
  };

  const handleNext = async () => {
    if (!started) {
      handleStart();
      return;
    }
    if (isLastStep) {
      setLoading(true);
      setError(null);
      try {
        // Convert keys to values before sending to server
        const convertedAnswers = convertAnswersToValues(answers);
        await settingsApi.saveSetupAnswers(convertedAnswers);
        navigate("/onboarding");
      } catch (err) {
        console.error("Failed to save setup:", err);
        setError("Failed to save settings. Please try again.");
      } finally {
        setLoading(false);
      }
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

  const isAdmin = user?.role === UserRole.Admin;

  if (!isAdmin) {
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
        }}
      >
        <Card
          elevation={6}
          sx={{
            width: "100%",
            maxWidth: 560,
            borderRadius: 3,
          }}
        >
          <CardContent sx={{ p: { xs: 3, sm: 4 } }}>
            <Alert severity="warning">{t("onlyAdminCanSetup")}</Alert>
          </CardContent>
        </Card>
      </Box>
    );
  }

  return (
    <Box
      sx={{
        position: "relative",
        width: "100%",
        minHeight: "100vh",
        display: "flex",
        flexDirection: "column",
        alignItems: "center",
        p: { xs: 2, sm: 4 },
        overflow: "auto",
        bgcolor: "background.default",
        "@keyframes floatA": {
          "0%": { transform: "translate3d(0, 0, 0) rotate(0deg)" },
          "25%": { transform: "translate3d(100px, -80px, 0) rotate(5deg)" },
          "50%": { transform: "translate3d(180px, 60px, 0) rotate(-3deg)" },
          "75%": { transform: "translate3d(60px, 120px, 0) rotate(4deg)" },
          "100%": { transform: "translate3d(0, 0, 0) rotate(0deg)" },
        },
        "@keyframes floatB": {
          "0%": { transform: "translate3d(0, 0, 0) rotate(0deg)" },
          "25%": { transform: "translate3d(-120px, 90px, 0) rotate(-4deg)" },
          "50%": { transform: "translate3d(-200px, -50px, 0) rotate(5deg)" },
          "75%": { transform: "translate3d(-80px, -130px, 0) rotate(-3deg)" },
          "100%": { transform: "translate3d(0, 0, 0) rotate(0deg)" },
        },
        "@keyframes floatC": {
          "0%": { transform: "translate3d(0, 0, 0) rotate(0deg)" },
          "25%": { transform: "translate3d(-150px, -100px, 0) rotate(6deg)" },
          "50%": { transform: "translate3d(90px, -180px, 0) rotate(-5deg)" },
          "75%": { transform: "translate3d(140px, 50px, 0) rotate(4deg)" },
          "100%": { transform: "translate3d(0, 0, 0) rotate(0deg)" },
        },
        "@keyframes floatD": {
          "0%": { transform: "translate3d(0, 0, 0) rotate(0deg)" },
          "25%": { transform: "translate3d(110px, 95px, 0) rotate(-5deg)" },
          "50%": { transform: "translate3d(-70px, 160px, 0) rotate(6deg)" },
          "75%": { transform: "translate3d(-160px, -40px, 0) rotate(-4deg)" },
          "100%": { transform: "translate3d(0, 0, 0) rotate(0deg)" },
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
          minHeight: { xs: "calc(100vh - 60px)", sm: 600 },
          height: { xs: "calc(100vh - 60px)", sm: "auto" },
          mt: "auto",
          mb: "auto",
          borderRadius: 3,
          backdropFilter: "blur(10px)",
          borderColor: (theme) =>
            theme.palette.mode === "dark"
              ? alpha(theme.palette.primary.main, 0.15)
              : "divider",
          borderWidth: 1,
          borderStyle: "solid",
          boxShadow: (theme) =>
            theme.palette.mode === "dark"
              ? `0 30px 190px ${alpha(
                  theme.palette.primary.main,
                  0.15,
                )}, 0 10px 40px ${alpha(theme.palette.common.black, 0.5)}`
              : `0 30px 60px ${alpha(
                  theme.palette.common.black,
                  0.12,
                )}, 0 10px 30px ${alpha(theme.palette.common.black, 0.08)}`,
          zIndex: 1,
        }}
      >
        <CardContent
          sx={{
            p: { xs: 3, sm: 4 },
            display: "flex",
            flexDirection: "column",
            minHeight: { xs: "calc(100vh - 80px)", sm: "calc(600px - 48px)" },
            maxHeight: { xs: "calc(100vh - 80px)", sm: "calc(700px - 48px)" },
            height: "100%",
            overflow: "auto",
          }}
        >
          <WizardHeader t={t} />

          <Box
            sx={{
              flex: 1,
              mt: 3.5,
              mb: 3.5,
            }}
          >
            <Fade in={true} timeout={600} key={started ? stepIndex : "intro"}>
              <Box>
                {started ? (
                  <Stack spacing={2.5}>
                    <WizardProgressBar
                      step={stepIndex + 1}
                      total={steps.length}
                    />
                    {currentStep?.render()}
                    {error && (
                      <Alert severity="error" onClose={() => setError(null)}>
                        {error}
                      </Alert>
                    )}
                  </Stack>
                ) : (
                  <Alert severity="info">{t("intro")}</Alert>
                )}
              </Box>
            </Fade>
          </Box>

          <Stack direction={{ xs: "column", sm: "row" }} spacing={2}>
            {started ? (
              <>
                <Button
                  variant="outlined"
                  size="large"
                  fullWidth
                  onClick={handleBack}
                  disabled={stepIndex === 0 || loading}
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
                  size="large"
                  fullWidth
                  onClick={handleNext}
                  disabled={!canProceed || loading}
                  sx={{
                    py: 1.3,
                    fontWeight: 700,
                    textTransform: "none",
                    position: "relative",
                  }}
                >
                  {loading ? (
                    <>
                      <CircularProgress
                        size={24}
                        sx={{
                          position: "absolute",
                          left: "50%",
                          marginLeft: "-12px",
                        }}
                      />
                      <span style={{ visibility: "hidden" }}>
                        {isLastStep ? t("actions.finish") : t("actions.next")}
                      </span>
                    </>
                  ) : (
                    <>{isLastStep ? t("actions.finish") : t("actions.next")}</>
                  )}
                </Button>
              </>
            ) : (
              <Button
                variant="contained"
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

      <Box
        sx={{
          mt: 1,
        }}
      >
        <Link
          href="https://github.com/bvdcode/cotton"
          target="_blank"
          rel="noopener noreferrer"
          underline="hover"
          sx={{
            color: "text.secondary",
            transition: "color 0.2s",
            "&:hover": {
              color: "primary.main",
            },
          }}
        >
          <Typography variant="body2">
            © {new Date().getFullYear()} Cotton Cloud • {t("common.author")}
          </Typography>
        </Link>
      </Box>
    </Box>
  );
}
