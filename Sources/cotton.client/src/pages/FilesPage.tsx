import {
  Alert,
  Box,
  Breadcrumbs,
  Button,
  IconButton,
  Link,
  LinearProgress,
  Paper,
  Stack,
  Typography,
} from "@mui/material";
import { ArrowBack, CreateNewFolder, Home as HomeIcon } from "@mui/icons-material";
import { useTranslation } from "react-i18next";
import type { FunctionComponent } from "react";
import { useState } from "react";

const FilesPage: FunctionComponent = () => {
  const { t } = useTranslation();

  // UI-only state (visual parity with the old page)
  const [selectedFile, setSelectedFile] = useState<File | null>(null);
  const [isUploading] = useState(false);
  const [progress] = useState(0);
  const [speedbps] = useState(0);
  const [error] = useState<string | null>(null);

  const onFileChange = (e: React.ChangeEvent<HTMLInputElement>) => {
    const f = e.target.files?.[0] ?? null;
    setSelectedFile(f);
  };

  return (
    <Box>
      <Box sx={{ mt: 2, display: "flex", justifyContent: "space-between" }}>
        <Typography variant="h4" gutterBottom>
          {t("filesPage.title")}
        </Typography>

        {/* Breadcrumbs (visual only) */}
        <Box sx={{ flex: 1, display: "flex", alignItems: "center", ml: 2 }}>
          <Breadcrumbs
            maxItems={5}
            itemsAfterCollapse={2}
            aria-label="breadcrumb"
            separator="/"
            sx={{ "& .MuiBreadcrumbs-separator": { color: "text.secondary" } }}
          >
            <Link
              underline="none"
              color="inherit"
              sx={{ display: "inline-flex", alignItems: "center" }}
              aria-label="root"
              title={t("filesPage.root", "Root")}
            >
              <HomeIcon fontSize="small" />
            </Link>
            <Typography color="text.primary" variant="body2">
              {t("filesPage.current", "Current")}
            </Typography>
          </Breadcrumbs>
        </Box>

        {/* Simulated loading/error placeholders for visual parity */}
        {/* Hide by default; shown here to keep layout consistent */}
        {/* <LinearProgress /> */}
        {/* <Alert severity="error">{t("filesPage.errorPlaceholder")}</Alert> */}

        <Stack
          direction={{ xs: "column", sm: "row" }}
          spacing={2}
          sx={{ mt: 2 }}
          alignItems="center"
        >
          <Typography variant="body2">
            {selectedFile ? selectedFile.name : t("filesPage.noFile")}
          </Typography>
          <Button variant="outlined" component="label">
            {selectedFile ? t("filesPage.changeFile") : t("filesPage.chooseFile")}
            <input hidden type="file" onChange={onFileChange} />
          </Button>
          <Button variant="contained" disabled>
            {isUploading ? t("filesPage.uploading") : t("filesPage.upload")}
          </Button>
          <IconButton title={t("filesPage.back")}
            disabled
          >
            <ArrowBack />
          </IconButton>
          <IconButton title={t("filesPage.newFolder")}
            disabled
          >
            <CreateNewFolder />
          </IconButton>
        </Stack>
      </Box>

      {isUploading && (
        <Box sx={{ mt: 2 }}>
          <LinearProgress variant="determinate" value={progress} />
          <Stack direction="row" spacing={2} sx={{ mt: 0.5 }}>
            <Typography variant="caption">{progress}%</Typography>
            <Typography variant="caption">
              {t("filesPage.threads", { count: 4 })}
            </Typography>
            <Typography variant="caption">
              {t("filesPage.speed", { speed: `${Math.round(speedbps)} B/s` })}
            </Typography>
          </Stack>
        </Box>
      )}

      {error && (
        <Box sx={{ mt: 2 }}>
          <Alert severity="error">{error}</Alert>
        </Box>
      )}

      <Box sx={{ mt: 4 }}>
        <Box
          sx={{
            mt: 1,
            display: "grid",
            gridTemplateColumns: "repeat(auto-fill, minmax(160px, 1fr))",
            gap: 2,
          }}
        >
          {/* Folders (visual placeholders) */}
          {[1, 2, 3].map((i) => (
            <Paper key={`folder-${i}`} elevation={2} sx={{ p: 1.5, display: "flex", flexDirection: "column", gap: 1 }}>
              <Box
                sx={{
                  width: "100%",
                  aspectRatio: "1 / 1",
                  bgcolor: "action.hover",
                  borderRadius: 1,
                  backgroundImage: `url('https://cdn-icons-png.flaticon.com/512/716/716784.png')`,
                  backgroundSize: "contain",
                  backgroundRepeat: "no-repeat",
                  backgroundPosition: "center",
                }}
              />
              <Box>
                <Typography variant="body2" noWrap title={t("filesPage.folder")}>{t("filesPage.folder")}</Typography>
                <Typography variant="caption" color="text.secondary">
                  {t("filesPage.folder")}
                </Typography>
              </Box>
            </Paper>
          ))}

          {/* Files (visual placeholders) */}
          {[1, 2].map((i) => (
            <Paper key={`file-${i}`} elevation={2} sx={{ p: 1.5, display: "flex", flexDirection: "column", gap: 1 }}>
              <Box
                sx={{
                  width: "100%",
                  aspectRatio: "1 / 1",
                  bgcolor: "action.hover",
                  borderRadius: 1,
                  backgroundImage: `url('https://images.freeimages.com/fic/images/icons/2813/flat_jewels/512/file.png')`,
                  backgroundSize: "contain",
                  backgroundRepeat: "no-repeat",
                  backgroundPosition: "center",
                }}
              />
              <Box>
                <Typography variant="body2" noWrap title="file.txt">file.txt</Typography>
                <Typography variant="caption" color="text.secondary">
                  text/plain
                </Typography>
              </Box>
              <Box>
                <Link href="#" onClick={(e) => e.preventDefault()}>{t("filesPage.download")}</Link>
              </Box>
            </Paper>
          ))}
        </Box>
      </Box>
    </Box>
  );
};

export default FilesPage;
