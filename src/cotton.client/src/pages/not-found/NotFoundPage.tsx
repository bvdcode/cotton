import { useTranslation } from "react-i18next";
import { useNavigate } from "react-router-dom";
import { Avatar, Box, Button, Container, Typography } from "@mui/material";

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
        <Avatar
          src="/assets/icons/icon.svg"
          alt=""
          sx={{ width: 56, height: 56, mx: "auto", mb: 3 }}
        />
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
        >
          {t("backButton")}
        </Button>
      </Box>
    </Container>
  );
};
