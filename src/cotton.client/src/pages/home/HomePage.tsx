import { useEffect } from "react";
import {
  Box,
  Card,
  CardContent,
  Typography,
  Alert,
} from "@mui/material";
import Loader from "../../shared/ui/Loader";
import { useLayoutsStore } from "../../shared/store/layoutsStore";

const formatBytes = (bytes: number): string => {
  if (!Number.isFinite(bytes) || bytes <= 0) return "0 B";
  const units = ["B", "KB", "MB", "GB", "TB"];
  let value = bytes;
  let unitIndex = 0;
  while (value >= 1024 && unitIndex < units.length - 1) {
    value /= 1024;
    unitIndex += 1;
  }
  const precision = unitIndex === 0 ? 0 : value < 10 ? 2 : 1;
  return `${value.toFixed(precision)} ${units[unitIndex]}`;
};

export const HomePage: React.FC = () => {
  const {
    rootNode,
    statsByLayoutId,
    loadingRoot,
    loadingStats,
    error,
    ensureHomeData,
  } = useLayoutsStore();

  const layoutId = rootNode?.layoutId;
  const stats = layoutId ? statsByLayoutId[layoutId] : undefined;

  useEffect(() => {
    void ensureHomeData();
  }, [ensureHomeData]);

  const isLoading = loadingRoot || loadingStats;

  if (isLoading && !stats) {
    return <Loader title="Loading" caption="Fetching layout stats…" />;
  }

  return (
    <Box p={3} width="100%">
      <Box mb={2}>
        <Typography variant="h4">Home</Typography>
        <Typography variant="body2" color="text.secondary">
          {rootNode ? `Layout: ${rootNode.name}` : "Layout: —"}
        </Typography>
      </Box>

      {error && (
        <Box mb={2}>
          <Alert severity="error">{error}</Alert>
        </Box>
      )}

      <Box
        sx={{
          display: "grid",
          gap: 2,
          gridTemplateColumns: {
            xs: "1fr",
            md: "repeat(3, 1fr)",
          },
        }}
      >
        <Card>
          <CardContent>
            <Typography variant="overline" color="text.secondary">
              Folders
            </Typography>
            <Typography variant="h4">
              {stats ? stats.nodeCount.toLocaleString() : "—"}
            </Typography>
            <Typography variant="caption" color="text.secondary">
              Total nodes in the layout
            </Typography>
          </CardContent>
        </Card>

        <Card>
          <CardContent>
            <Typography variant="overline" color="text.secondary">
              Files
            </Typography>
            <Typography variant="h4">
              {stats ? stats.fileCount.toLocaleString() : "—"}
            </Typography>
            <Typography variant="caption" color="text.secondary">
              Total files across all nodes
            </Typography>
          </CardContent>
        </Card>

        <Card>
          <CardContent>
            <Typography variant="overline" color="text.secondary">
              Data
            </Typography>
            <Typography variant="h4">
              {stats ? formatBytes(stats.sizeBytes) : "—"}
            </Typography>
            <Typography variant="caption" color="text.secondary">
              Sum of file sizes
            </Typography>
          </CardContent>
        </Card>
      </Box>

      {isLoading && stats && (
        <Box mt={2}>
          <Typography variant="caption" color="text.secondary">
            Refreshing…
          </Typography>
        </Box>
      )}
    </Box>
  );
};
