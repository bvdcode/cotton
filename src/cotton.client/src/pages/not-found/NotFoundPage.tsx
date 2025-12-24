import { useTranslation } from "react-i18next";
import { useNavigate } from "react-router-dom";
import { Button, Typography, Container, Box } from "@mui/material";

export const NotFoundPage = () => {
  const navigate = useNavigate();
  const { t } = useTranslation("notFound");

  return (
    <Container
      maxWidth="sm"
      sx={{
        height: "100%",
        display: "flex",
        alignItems: "center",
        justifyContent: "center",
      }}
    >
      <Box
        sx={{
          textAlign: "center",
          py: 6,
          px: 4,
        }}
      >
        <Typography
          variant="h1"
          component="h1"
          gutterBottom
          sx={{
            fontSize: "6rem",
            fontWeight: 700,
            color: "text.primary",
            mb: 2,
          }}
        >
          404
        </Typography>
        <Typography
          variant="h4"
          component="h2"
          gutterBottom
          sx={{
            mb: 2,
            fontWeight: 500,
          }}
        >
          {t("title")}
        </Typography>
        <Typography
          variant="body1"
          color="text.secondary"
          sx={{
            mb: 4,
            fontSize: "1.1rem",
          }}
        >
          {t("message")}
        </Typography>
        <Button
          variant="contained"
          color="primary"
          size="large"
          onClick={() => navigate("/")}
          sx={{
            px: 4,
            py: 1.5,
            borderRadius: 2,
            textTransform: "none",
            fontSize: "1rem",
          }}
        >
          {t("backButton")}
        </Button>
      </Box>
    </Container>
  );
};
