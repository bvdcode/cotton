import { Box, Button, Typography, Stack, alpha } from "@mui/material";
import { useNavigate } from "react-router-dom";
import { Cloud, Storage, Security, Speed } from "@mui/icons-material";
import { useState, useEffect } from "react";
import { useTranslation } from "react-i18next";
import { useServerSettings } from "../../shared/store/useServerSettings";

const features = [
  { icon: <Cloud />, key: "cloud" },
  { icon: <Storage />, key: "storage" },
  { icon: <Security />, key: "security" },
  { icon: <Speed />, key: "speed" },
];

export function OnboardingPage() {
  const navigate = useNavigate();
  const { t } = useTranslation("onboarding");
  const { fetchSettings } = useServerSettings();
  const [currentFeature, setCurrentFeature] = useState(0);

  useEffect(() => {
    const interval = setInterval(() => {
      setCurrentFeature((prev) => (prev + 1) % features.length);
    }, 5000);

    return () => clearInterval(interval);
  }, []);

  const handleSkip = async () => {
    await fetchSettings({ force: true });
    navigate("/");
  };

  return (
    <Box
      sx={{
        position: "relative",
        width: "100%",
        minHeight: "100vh",
        display: "flex",
        flexDirection: "column",
        alignItems: "center",
        justifyContent: "center",
        bgcolor: "background.default",
        overflow: "hidden",
        "@keyframes floatFeature": {
          "0%, 100%": { transform: "translateY(0)" },
          "50%": { transform: "translateY(-20px)" },
        },
        "@keyframes pulse": {
          "0%, 100%": { opacity: 0.6, transform: "scale(1)" },
          "50%": { opacity: 1, transform: "scale(1.05)" },
        },
      }}
    >
      {/* Animated background blobs */}
      <Box
        sx={{
          position: "absolute",
          top: "10%",
          left: "10%",
          width: 300,
          height: 300,
          borderRadius: "50%",
          background: (theme) =>
            `radial-gradient(circle, ${alpha(
              theme.palette.primary.main,
              0.2,
            )}, transparent)`,
          animation: "pulse 4s ease-in-out infinite",
        }}
      />
      <Box
        sx={{
          position: "absolute",
          bottom: "15%",
          right: "15%",
          width: 250,
          height: 250,
          borderRadius: "50%",
          background: (theme) =>
            `radial-gradient(circle, ${alpha(
              theme.palette.secondary.main,
              0.2,
            )}, transparent)`,
          animation: "pulse 5s ease-in-out infinite",
          animationDelay: "1s",
        }}
      />

      <Stack
        spacing={6}
        alignItems="center"
        sx={{
          zIndex: 1,
          maxWidth: 600,
          px: 3,
        }}
      >
        {/* Logo/Title */}
        <Typography
          variant="h2"
          sx={{
            fontWeight: 700,
            background: (theme) =>
              `linear-gradient(45deg, ${theme.palette.primary.main}, ${theme.palette.secondary.main})`,
            WebkitBackgroundClip: "text",
            WebkitTextFillColor: "transparent",
          }}
        >
          Cotton Cloud
        </Typography>

        {/* Feature showcase */}
        <Box
          sx={{
            position: "relative",
            width: 200,
            height: 200,
            display: "flex",
            alignItems: "center",
            justifyContent: "center",
          }}
        >
          {features.map((feature, index) => (
            <Box
              key={feature.key}
              sx={{
                position: "absolute",
                display: "flex",
                alignItems: "center",
                justifyContent: "center",
                width: 120,
                height: 120,
                borderRadius: "50%",
                bgcolor: (theme) => alpha(theme.palette.primary.main, 0.1),
                border: (theme) => `3px solid ${theme.palette.primary.main}`,
                transition: "all 0.5s ease-in-out",
                opacity: currentFeature === index ? 1 : 0,
                transform: currentFeature === index ? "scale(1)" : "scale(0.8)",
                animation:
                  currentFeature === index
                    ? "floatFeature 3s ease-in-out infinite"
                    : "none",
                "& svg": {
                  fontSize: 64,
                  color: "primary.main",
                },
              }}
            >
              {feature.icon}
            </Box>
          ))}
        </Box>

        {/* Feature descriptions */}
        <Box sx={{ textAlign: "center", minHeight: 120 }}>
          {features.map((feature, index) => (
            <Box
              key={feature.key}
              sx={{
                opacity: currentFeature === index ? 1 : 0,
                transition: "opacity 0.5s ease-in-out",
                position: currentFeature === index ? "relative" : "absolute",
                visibility: currentFeature === index ? "visible" : "hidden",
              }}
            >
              <Typography variant="h4" gutterBottom sx={{ fontWeight: 600 }}>
                {t(`features.${feature.key}.title`)}
              </Typography>
              <Typography variant="body1" color="text.secondary">
                {t(`features.${feature.key}.description`)}
              </Typography>
            </Box>
          ))}
        </Box>

        {/* Progress dots */}
        <Stack direction="row" spacing={1.5}>
          {features.map((_, index) => (
            <Box
              key={index}
              sx={{
                width: 12,
                height: 12,
                borderRadius: "50%",
                bgcolor: currentFeature === index ? "primary.main" : "divider",
                transition: "all 0.3s ease-in-out",
                transform: currentFeature === index ? "scale(1.2)" : "scale(1)",
              }}
            />
          ))}
        </Stack>

        {/* Skip button */}
        <Button
          variant="contained"
          size="large"
          onClick={handleSkip}
          sx={{
            px: 6,
            py: 1.5,
            fontWeight: 700,
            textTransform: "none",
          }}
        >
          {t("skip")}
        </Button>
      </Stack>
    </Box>
  );
}
