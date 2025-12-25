import {
  Box,
  Button,
  Card,
  CardContent,
  Stack,
  Typography,
} from "@mui/material";
import { useServerSettings } from "../../shared/store/useServerSettings";

export function SetupWizardPage() {
  const { data, loading, fetchSettings } = useServerSettings();

  return (
    <Box
      sx={{
        width: "100%",
        height: "100vh",
        display: "flex",
        alignItems: "center",
        justifyContent: "center",
        bgcolor: "linear-gradient(135deg, #101520, #0c1a2a)",
        background:
          "radial-gradient(circle at 20% 20%, rgba(76,110,245,0.15), transparent 35%)," +
          "radial-gradient(circle at 80% 10%, rgba(76,245,181,0.12), transparent 30%)," +
          "radial-gradient(circle at 50% 80%, rgba(245,186,76,0.10), transparent 30%)," +
          "#0c111b",
        color: "#f7f9fb",
        p: { xs: 2, sm: 4 },
      }}
    >
      <Card
        elevation={6}
        sx={{
          width: "100%",
          maxWidth: 760,
          borderRadius: 3,
          backdropFilter: "blur(8px)",
          background:
            "linear-gradient(145deg, rgba(18,28,45,0.9), rgba(17,25,39,0.92))",
          border: "1px solid rgba(255,255,255,0.08)",
          boxShadow: "0 20px 60px rgba(0,0,0,0.45)",
        }}
      >
        <CardContent sx={{ p: { xs: 3, sm: 4 }, color: "#e8eef7" }}>
          <Stack spacing={3}>
            <Stack spacing={1}>
              <Typography variant="h4" fontWeight={700} color="#fefefe">
                Server setup wizard
              </Typography>
              <Typography variant="body1" color="rgba(232,238,247,0.78)">
                Complete initial server configuration to start using the app. We
                detected that the server is not initialized yet.
              </Typography>
            </Stack>

            <Stack direction={{ xs: "column", sm: "row" }} spacing={2}>
              <Button
                variant="outlined"
                color="inherit"
                size="large"
                fullWidth
                onClick={() => fetchSettings({ force: true })}
                disabled={loading}
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
                Refresh status
              </Button>
              <Button
                variant="contained"
                color="primary"
                size="large"
                fullWidth
                sx={{
                  py: 1.3,
                  fontWeight: 700,
                  textTransform: "none",
                  boxShadow: "0 8px 24px rgba(76,110,245,0.35)",
                }}
              >
                Start initialization
              </Button>
            </Stack>

            <Typography variant="body2" color="rgba(232,238,247,0.6)">
              This wizard is a placeholder for the server initialization flow.
              Plug your steps here (admin creation, storage config, etc.).
            </Typography>
          </Stack>
        </CardContent>
      </Card>
    </Box>
  );
}

function StatusTile({ label, value }: { label: string; value: string }) {
  return (
    <Box
      sx={{
        border: "1px solid rgba(255,255,255,0.08)",
        borderRadius: 2,
        p: 2,
        bgcolor: "rgba(255,255,255,0.02)",
        minHeight: 86,
      }}
    >
      <Typography variant="body2" color="rgba(232,238,247,0.68)">
        {label}
      </Typography>
      <Typography variant="h6" fontWeight={700} color="#fefefe">
        {value}
      </Typography>
    </Box>
  );
}
