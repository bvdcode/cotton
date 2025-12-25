import {
  Box,
  Button,
  Card,
  CardContent,
  Stack,
  Typography,
} from "@mui/material";
import { useCallback, useMemo, useState, type ReactNode } from "react";
import { useTranslation } from "react-i18next";
import { useNavigate } from "react-router-dom";
import OpenInNewIcon from "@mui/icons-material/OpenInNew";
import {
  setupStepDefinitions,
  type SetupMultiOption,
  type SetupSingleOption,
} from "./setupQuestions";

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
        const options: Array<SetupSingleOption<boolean> & { label: string; description?: string }> = def.options.map(
          (opt) => ({
            ...opt,
            label: t(opt.labelKey),
            description: opt.descriptionKey ? t(opt.descriptionKey) : undefined,
          }),
        );

        return {
          key: def.key,
          render: () => (
            <QuestionBlock
              title={t(def.titleKey)}
              subtitle={t(def.subtitleKey)}
              linkUrl={def.linkUrl}
              linkAriaLabel={def.linkAriaKey ? t(def.linkAriaKey) : undefined}
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
          ...opt,
          label: t(opt.labelKey),
        }));

        return {
          key: def.key,
          render: () => (
            <QuestionBlockMulti
              title={t(def.titleKey)}
              subtitle={t(def.subtitleKey)}
              options={options}
              selectedKeys={intendedUse}
              onToggle={toggleIntendedUse}
            />
          ),
          isValid: (): boolean => intendedUse.length > 0,
        };
      }

      if (def.type === "single" && def.key === "telemetry") {
        const options: Array<SetupSingleOption<boolean> & { label: string; description?: string }> = def.options.map(
          (opt) => ({
            ...opt,
            label: t(opt.labelKey),
            description: opt.descriptionKey ? t(opt.descriptionKey) : undefined,
          }),
        );

        return {
          key: def.key,
          render: () => (
            <QuestionBlock
              title={t(def.titleKey)}
              subtitle={t(def.subtitleKey)}
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
  }, [
    t,
    multiuserChoiceKey,
    intendedUse,
    allowTelemetry,
    toggleIntendedUse,
  ]);

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
        color: "#f7f9fb",
        p: { xs: 2, sm: 4 },
        overflow: "hidden",
        background: "linear-gradient(135deg, #0c111b, #101826)",
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
          borderRadius: 3,
          backdropFilter: "blur(10px)",
          background:
            "linear-gradient(155deg, rgba(18,28,45,0.85), rgba(14,23,37,0.92))",
          border: "1px solid rgba(255,255,255,0.08)",
          boxShadow: "0 30px 90px rgba(0,0,0,0.55)",
          zIndex: 1,
        }}
      >
        <CardContent sx={{ p: { xs: 3, sm: 4 }, color: "#e8eef7" }}>
          <Stack spacing={3.5}>
            <Header t={t} />

            {started ? (
              <Stack spacing={2.5}>
                <ProgressBar step={stepIndex + 1} total={steps.length} />
                {currentStep?.render()}
                <Stack direction={{ xs: "column", sm: "row" }} spacing={2}>
                  <Button
                    variant="outlined"
                    color="inherit"
                    size="large"
                    fullWidth
                    onClick={handleBack}
                    sx={{
                      py: 1.3,
                      fontWeight: 700,
                      textTransform: "none",
                      borderColor: "rgba(255,255,255,0.4)",
                      color: "rgba(255,255,255,0.9)",
                      ":hover": {
                        borderColor: "rgba(255,255,255,0.7)",
                        backgroundColor: "rgba(255,255,255,0.06)",
                      },
                    }}
                  >
                    {stepIndex === 0 ? t("actions.cancel") : t("actions.back")}
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
                      boxShadow: "0 10px 26px rgba(76,110,245,0.35)",
                    }}
                  >
                    {isLastStep ? t("actions.finish") : t("actions.next")}
                  </Button>
                </Stack>
              </Stack>
            ) : (
              <Stack spacing={2.5}>
                <Typography variant="body1" color="rgba(232,238,247,0.78)">
                  {t("intro")}
                </Typography>
                <Stack direction={{ xs: "column", sm: "row" }} spacing={2}>
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
                      boxShadow: "0 10px 26px rgba(76,110,245,0.35)",
                    }}
                  >
                    {t("actions.start")}
                  </Button>
                </Stack>
              </Stack>
            )}
          </Stack>
        </CardContent>
      </Card>
    </Box>
  );
}

function Header({ t }: { t: (key: string) => string }) {
  return (
    <Stack spacing={1.5}>
      <Stack spacing={0.5}>
        <Typography variant="h4" fontWeight={800} color="#fefefe">
          {t("title")}
        </Typography>
        <Typography variant="body1" color="rgba(232,238,247,0.82)">
          {t("subtitle")}
        </Typography>
      </Stack>
    </Stack>
  );
}

function QuestionBlock<T>({
  title,
  subtitle,
  options,
  selectedValue,
  selectedKey,
  onSelect,
  linkUrl,
  linkAriaLabel,
}: {
  title: string;
  subtitle: string;
  options: Array<SetupSingleOption<T> & { label: string; description?: string }>;
  selectedValue?: T | null;
  selectedKey?: string | null;
  onSelect: (key: string, value: T) => void;
  linkUrl?: string;
  linkAriaLabel?: string;
}) {
  return (
    <Stack spacing={1.5}>
      <QuestionHeader
        title={title}
        subtitle={subtitle}
        linkUrl={linkUrl}
        linkAriaLabel={linkAriaLabel}
      />
      <Box
        sx={{
          display: "grid",
          gridTemplateColumns: { xs: "1fr", sm: "repeat(3, 1fr)" },
          gap: 1.5,
        }}
      >
        {options.map((opt) => {
          const active = selectedKey ? selectedKey === opt.key : selectedValue === opt.value;
          return (
            <OptionCard
              key={opt.key}
              label={opt.label}
              description={opt.description}
              active={active}
              onClick={() => onSelect(opt.key, opt.value)}
            />
          );
        })}
      </Box>
    </Stack>
  );
}

function QuestionBlockMulti({
  title,
  subtitle,
  options,
  selectedKeys,
  onToggle,
}: {
  title: string;
  subtitle: string;
  options: Array<SetupMultiOption & { label: string }>;
  selectedKeys: string[];
  onToggle: (key: string) => void;
}) {
  return (
    <Stack spacing={1.5}>
      <QuestionHeader title={title} subtitle={subtitle} />
      <Box
        sx={{
          display: "grid",
          gridTemplateColumns: { xs: "1fr", sm: "repeat(3, 1fr)" },
          gap: 1.5,
        }}
      >
        {options.map((opt) => {
          const active = selectedKeys.includes(opt.key);
          return (
            <OptionCard
              key={opt.key}
              label={opt.label}
              active={active}
              onClick={() => onToggle(opt.key)}
            />
          );
        })}
      </Box>
    </Stack>
  );
}

function QuestionHeader({
  title,
  subtitle,
  linkUrl,
  linkAriaLabel,
}: {
  title: string;
  subtitle: string;
  linkUrl?: string;
  linkAriaLabel?: string;
}) {
  return (
    <Stack spacing={0.4} direction="row" alignItems="center" justifyContent="space-between">
      <Stack spacing={0.4}>
        <Typography variant="h6" fontWeight={700} color="#fdfefe">
          {title}
        </Typography>
        <Typography variant="body2" color="rgba(232,238,247,0.74)">
          {subtitle}
        </Typography>
      </Stack>
      {linkUrl ? (
        <Button
          href={linkUrl}
          target="_blank"
          rel="noreferrer"
          variant="text"
          size="small"
          aria-label={linkAriaLabel}
          sx={{
            minWidth: 0,
            color: "rgba(255,255,255,0.75)",
            ":hover": { color: "rgba(255,255,255,0.95)" },
            p: 0.75,
            borderRadius: 1.5,
          }}
        >
          <OpenInNewIcon fontSize="small" />
        </Button>
      ) : null}
    </Stack>
  );
}

function OptionCard({
  label,
  description,
  active,
  onClick,
}: {
  label: string;
  description?: string;
  active: boolean;
  onClick: () => void;
}) {
  return (
    <Box
      role="button"
      tabIndex={0}
      onClick={onClick}
      onKeyDown={(e) => {
        if (e.key === "Enter" || e.key === " ") {
          e.preventDefault();
          onClick();
        }
      }}
      sx={{
        borderRadius: 2,
        p: 2,
        minHeight: 120,
        border: active
          ? "1.5px solid rgba(92, 202, 255, 0.9)"
          : "1px solid rgba(255,255,255,0.08)",
        background: active
          ? "linear-gradient(145deg, rgba(76,110,245,0.16), rgba(76,245,181,0.12))"
          : "rgba(255,255,255,0.02)",
        boxShadow: active
          ? "0 15px 35px rgba(76,110,245,0.25)"
          : "0 6px 18px rgba(0,0,0,0.25)",
        cursor: "pointer",
        display: "flex",
        flexDirection: "column",
        gap: 0.6,
        transition: "all 0.2s ease",
        ":hover": {
          borderColor: "rgba(92,202,255,0.8)",
          transform: "translateY(-2px)",
        },
        outline: "none",
      }}
    >
      <Typography variant="subtitle1" fontWeight={700} color="#fefefe">
        {label}
      </Typography>
      {description ? (
        <Typography variant="body2" color="rgba(232,238,247,0.7)">
          {description}
        </Typography>
      ) : null}
    </Box>
  );
}

function ProgressBar({ step, total }: { step: number; total: number }) {
  const progress = Math.round((step / total) * 100);
  return (
    <Stack spacing={0.5}>
      <Typography variant="body2" color="rgba(232,238,247,0.7)">
        {progress}% · {step}/{total}
      </Typography>
      <Box
        sx={{
          width: "100%",
          height: 8,
          borderRadius: 999,
          background: "rgba(255,255,255,0.08)",
          overflow: "hidden",
        }}
      >
        <Box
          sx={{
            height: "100%",
            width: `${progress}%`,
            background: "linear-gradient(90deg, #4c6ef5, #4cf5b5)",
            transition: "width 0.25s ease",
          }}
        />
      </Box>
    </Stack>
  );
}

function FloatingBlobs() {
  return (
    <Box
      aria-hidden
      sx={{
        position: "absolute",
        inset: 0,
        pointerEvents: "none",
        overflow: "hidden",
        zIndex: 0,
      }}
    >
      <Blob
        size={360}
        sx={{
          top: "12%",
          left: "14%",
          background:
            "radial-gradient(circle, rgba(76,110,245,0.45), transparent 60%)",
          animation: "floatA 14s ease-in-out infinite",
        }}
      />
      <Blob
        size={420}
        sx={{
          bottom: "-4%",
          right: "-6%",
          background:
            "radial-gradient(circle, rgba(76,245,181,0.35), transparent 60%)",
          animation: "floatB 18s ease-in-out infinite",
        }}
      />
      <Blob
        size={280}
        sx={{
          top: "40%",
          right: "20%",
          background:
            "radial-gradient(circle, rgba(245,186,76,0.25), transparent 65%)",
          animation: "floatC 16s ease-in-out infinite",
        }}
      />
    </Box>
  );
}

function Blob({ size, sx }: { size: number; sx: object }) {
  return (
    <Box
      sx={{
        position: "absolute",
        width: size,
        height: size,
        filter: "blur(45px)",
        opacity: 0.8,
        ...sx,
      }}
    />
  );
}
