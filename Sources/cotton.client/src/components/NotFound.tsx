import React from "react";
import { useNavigate } from "react-router-dom";
import { useTranslation } from "react-i18next";
import { Box, Button, Typography } from "@mui/material";

const NotFound: React.FC = () => {
  const navigate = useNavigate();
  const { t } = useTranslation();

  return (
    <Box
      sx={{
        height: "100%",
        display: "flex",
        flexDirection: "column",
        alignItems: "center",
        justifyContent: "center",
        gap: 2,
        textAlign: "center",
        p: 2,
      }}
    >
      <Typography variant="h1" sx={{ fontSize: "6rem", fontWeight: "bold" }}>
        404
      </Typography>
      <Typography variant="h5">{t("notFound.title")}</Typography>
      <Typography variant="body1" sx={{ color: "text.secondary", mb: 2 }}>
        {t("notFound.description")}
      </Typography>
      <Button variant="contained" onClick={() => navigate("/")}>
        {t("notFound.buttonText")}
      </Button>
    </Box>
  );
};

export default NotFound;
