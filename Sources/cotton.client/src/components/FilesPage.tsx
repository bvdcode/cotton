import { useTranslation } from "react-i18next";
import { useState, useMemo } from "react";
import Box from "@mui/material/Box";
import Typography from "@mui/material/Typography";
import Button from "@mui/material/Button";
import LinearProgress from "@mui/material/LinearProgress";
import Stack from "@mui/material/Stack";
import List from "@mui/material/List";
import ListItem from "@mui/material/ListItem";
import ListItemText from "@mui/material/ListItemText";
import Alert from "@mui/material/Alert";
import { useSettings } from "../stores/settingsStore.ts";
import { normalizeAlgorithm, hashBlob } from "../utils/hash.ts";
import { chunkBlob } from "../utils/chunk.ts";
import { uploadChunk } from "../api/files.ts";

const FilesPage = () => {
  const { t } = useTranslation();
  const settings = useSettings((s) => s.settings);
  const loadingSettings = useSettings((s) => s.loading);
  const errorSettings = useSettings((s) => s.error);

  const [selectedFile, setSelectedFile] = useState<File | null>(null);
  const [progress, setProgress] = useState<number>(0);
  const [hashes, setHashes] = useState<string[]>([]);
  const [error, setError] = useState<string | null>(null);
  const [isUploading, setIsUploading] = useState(false);

  const algo: string | null = useMemo(() => {
    if (!settings) return null;
    try {
      return normalizeAlgorithm(settings.supportedHashAlgorithm) as string;
    } catch {
      return null;
    }
  }, [settings]);

  const onFileChange = (e: React.ChangeEvent<HTMLInputElement>) => {
    const f = e.target.files?.[0] ?? null;
    setSelectedFile(f);
    setHashes([]);
    setProgress(0);
    setError(null);
  };

  const onUpload = async () => {
    if (!selectedFile) return;
    if (!settings || !algo) {
      setError(t("files.noSettings", "Settings not loaded or unsupported algorithm."));
      return;
    }
    setIsUploading(true);
    setError(null);
    const total = Math.ceil(selectedFile.size / settings.maxChunkSizeBytes);
    const hashesLocal: string[] = [];
    let index = 0;
    try {
      for (const chunk of chunkBlob(selectedFile, settings.maxChunkSizeBytes)) {
        const h = await hashBlob(chunk, algo);
        await uploadChunk(chunk, h, selectedFile.name);
        hashesLocal.push(h);
        index++;
        setProgress(Math.round((index / total) * 100));
      }
      setHashes(hashesLocal);
    } catch (_e) {
      const msg = _e instanceof Error ? _e.message : String(_e);
      setError(msg);
    } finally {
      setIsUploading(false);
    }
  };

  return (
    <Box>
      <Typography variant="h4" gutterBottom>
        {t("files.title", "Files")}
      </Typography>
      <Typography color="text.secondary">
        {t("files.subtitle", "Manage and browse your files here.")}
      </Typography>
      <Box sx={{ mt: 2 }}>
        {loadingSettings && <LinearProgress />}
        {errorSettings && <Alert severity="error">{errorSettings}</Alert>}
        {settings && (
          <Alert severity="info">
            {t("files.settings", "Chunk size: {{size}} bytes, Algorithm: {{algo}}", {
              size: settings.maxChunkSizeBytes,
              algo: settings.supportedHashAlgorithm,
            })}
          </Alert>
        )}
      </Box>
      <Stack direction={{ xs: "column", sm: "row" }} spacing={2} sx={{ mt: 2 }} alignItems="center">
        <Button variant="outlined" component="label" disabled={isUploading}>
          {selectedFile ? t("files.changeFile", "Change file") : t("files.chooseFile", "Choose file")}
          <input hidden type="file" onChange={onFileChange} />
        </Button>
        <Typography variant="body2" sx={{ minWidth: 200 }}>
          {selectedFile ? selectedFile.name : t("files.noFile", "No file selected")}
        </Typography>
        <Button variant="contained" onClick={onUpload} disabled={!selectedFile || !settings || isUploading}>
          {isUploading ? t("files.uploading", "Uploading...") : t("files.upload", "Upload")}
        </Button>
      </Stack>
      {isUploading && (
        <Box sx={{ mt: 2 }}>
          <LinearProgress variant="determinate" value={progress} />
          <Typography variant="caption">{progress}%</Typography>
        </Box>
      )}
      {error && (
        <Box sx={{ mt: 2 }}>
          <Alert severity="error">{error}</Alert>
        </Box>
      )}
      {hashes.length > 0 && (
        <Box sx={{ mt: 3 }}>
          <Typography variant="h6">{t("files.hashList", "Chunk hashes (ordered)")}</Typography>
          <List dense>
            {hashes.map((h, i) => (
              <ListItem key={`${i}-${h}`}>
                <ListItemText primary={`#${i + 1}`} secondary={h} />
              </ListItem>
            ))}
          </List>
        </Box>
      )}
    </Box>
  );
};

export default FilesPage;
