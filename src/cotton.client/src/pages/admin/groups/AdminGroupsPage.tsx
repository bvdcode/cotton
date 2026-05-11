import { Alert, Box, Paper, Stack, Typography } from "@mui/material";
import { useTranslation } from "react-i18next";

export const AdminGroupsPage = () => {
  const { t } = useTranslation("admin");

  return (
    <Stack>
      <Box sx={{ width: "100%", display: "flex", justifyContent: "center" }}>
        <Paper
          sx={{
            width: "min(100%, 880px)",
            overflow: "hidden",
          }}
        >
          <Stack p={3} spacing={2}>
            <Typography variant="h5" fontWeight={700}>
              {t("groups.title", { defaultValue: "Groups" })}
            </Typography>
            <Typography variant="body2" color="text.secondary">
              {t("groups.description", {
                defaultValue:
                  "Manage access groups separately from individual users.",
              })}
            </Typography>
            <Alert severity="info">
              {t("groups.inDevelopment", {
                defaultValue: "Group management is in development.",
              })}
            </Alert>
          </Stack>
        </Paper>
      </Box>
    </Stack>
  );
};
