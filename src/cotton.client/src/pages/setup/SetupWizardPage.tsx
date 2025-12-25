import {
  Box,
  Button,
  Card,
  CardContent,
  Stack,
  Typography,
} from "@mui/material";
import { useMemo, useState } from "react";
import { useTranslation } from "react-i18next";

type SingleOption<T> = {
  key: string;
  label: string;
  description?: string;
  value: T;
};

type MultiOption = {
  key: string;
  label: string;
};

export function SetupWizardPage() {
  const { t } = useTranslation("setup");
  const [unsafeMultiuserInteraction, setUnsafeMultiuserInteraction] =
    useState<boolean | null>(null);
  const [intendedUse, setIntendedUse] = useState<string[]>([]);
  const [allowTelemetry, setAllowTelemetry] = useState<boolean | null>(null);

  const multiuserOptions: SingleOption<boolean>[] = useMemo(
    () => [
      {
        key: "family",
        label: t("questions.multiuser.options.family"),
        value: true,
        description: t("questions.multiuser.descriptions.family"),
      },
      {
        key: "many",
        label: t("questions.multiuser.options.many"),
        value: false,
        description: t("questions.multiuser.descriptions.many"),
      },
      {
        key: "unknown",
        label: t("questions.multiuser.options.unknown"),
        value: false,
        description: t("questions.multiuser.descriptions.unknown"),
      },
    ],
    [t],
  );

  const usageOptions: MultiOption[] = useMemo(
    () => [
      { key: "photos", label: t("questions.usage.options.photos") },
      { key: "documents", label: t("questions.usage.options.documents") },
      { key: "media", label: t("questions.usage.options.media") },
    ],
    [t],
  );

  const telemetryOptions: SingleOption<boolean>[] = useMemo(
    () => [
      { key: "allow", label: t("questions.telemetry.options.allow"), value: true },
      { key: "deny", label: t("questions.telemetry.options.deny"), value: false },
    ],
    [t],
  );

  const toggleIntendedUse = (key: string) => {
    setIntendedUse((prev) =>
      prev.includes(key) ? prev.filter((k) => k !== key) : [...prev, key],
    );
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

            <QuestionBlock
              title={t("questions.multiuser.title")}
              subtitle={t("questions.multiuser.subtitle")}
              options={multiuserOptions}
              selectedValue={unsafeMultiuserInteraction}
              onSelect={(v) => setUnsafeMultiuserInteraction(v)}
            />

            <QuestionBlockMulti
              title={t("questions.usage.title")}
              subtitle={t("questions.usage.subtitle")}
              options={usageOptions}
              selectedKeys={intendedUse}
              onToggle={toggleIntendedUse}
            />

            <QuestionBlock
              title={t("questions.telemetry.title")}
              subtitle={t("questions.telemetry.subtitle")}
              options={telemetryOptions}
              selectedValue={allowTelemetry}
              onSelect={(v) => setAllowTelemetry(v)}
            />

            <Stack direction={{ xs: "column", sm: "row" }} spacing={2}>
              <Button
                variant="contained"
                color="primary"
                size="large"
                fullWidth
                sx={{
                  py: 1.3,
                  fontWeight: 700,
                  textTransform: "none",
                  boxShadow: "0 10px 26px rgba(76,110,245,0.35)",
                }}
              >
                {t("actions.start")}
              </Button>
              <Button
                variant="outlined"
                color="inherit"
                size="large"
                fullWidth
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
                {t("actions.later")}
              </Button>
            </Stack>
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
      <Button
        href="https://github.com/bvdcode/cotton"
        target="_blank"
        rel="noreferrer"
        variant="text"
        size="small"
        sx={{
          alignSelf: "flex-start",
          px: 0,
          color: "rgba(255,255,255,0.72)",
          textTransform: "none",
          fontWeight: 600,
          gap: 0.75,
          ":hover": { color: "rgba(255,255,255,0.95)" },
        }}
      >
        {t("repoLink")}
      </Button>
    </Stack>
  );
}

function QuestionBlock<T>({
  title,
  subtitle,
  options,
  selectedValue,
  onSelect,
}: {
  title: string;
  subtitle: string;
  options: SingleOption<T>[];
  selectedValue: T | null;
  onSelect: (value: T) => void;
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
          const active = selectedValue === opt.value;
          return (
            <OptionCard
              key={opt.key}
              label={opt.label}
              description={opt.description}
              active={active}
              onClick={() => onSelect(opt.value)}
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
  options: MultiOption[];
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

function QuestionHeader({ title, subtitle }: { title: string; subtitle: string }) {
  return (
    <Stack spacing={0.4}>
      <Typography variant="h6" fontWeight={700} color="#fdfefe">
        {title}
      </Typography>
      <Typography variant="body2" color="rgba(232,238,247,0.74)">
        {subtitle}
      </Typography>
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
          background: "radial-gradient(circle, rgba(76,110,245,0.45), transparent 60%)",
          animation: "floatA 14s ease-in-out infinite",
        }}
      />
      <Blob
        size={420}
        sx={{
          bottom: "-4%",
          right: "-6%",
          background: "radial-gradient(circle, rgba(76,245,181,0.35), transparent 60%)",
          animation: "floatB 18s ease-in-out infinite",
        }}
      />
      <Blob
        size={280}
        sx={{
          top: "40%",
          right: "20%",
          background: "radial-gradient(circle, rgba(245,186,76,0.25), transparent 65%)",
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
