import { useTranslation } from "react-i18next";
import { useState, useMemo, useEffect } from "react";
import Box from "@mui/material/Box";
import Typography from "@mui/material/Typography";
import Button from "@mui/material/Button";
import LinearProgress from "@mui/material/LinearProgress";
import Stack from "@mui/material/Stack";
import Alert from "@mui/material/Alert";
import { useSettings } from "../stores/settingsStore.ts";
import { normalizeAlgorithm, hashBlob } from "../utils/hash.ts";
import { chunkBlob } from "../utils/chunk.ts";
import { UPLOAD_CONCURRENCY_DEFAULT } from "../config.ts";
import { formatBytes, formatBytesPerSecond } from "../utils/format";
import {
  uploadChunk,
  createFileFromChunks,
  listFiles,
  getDownloadUrl,
  type FileManifestDto,
} from "../api/files.ts";
import Paper from "@mui/material/Paper";
import Link from "@mui/material/Link";
// Removed duplicate import of hashBlob

const FilesPage = () => {
  const { t } = useTranslation();
  const settings = useSettings((s) => s.settings);
  const loadingSettings = useSettings((s) => s.loading);
  const errorSettings = useSettings((s) => s.error);

  const [selectedFile, setSelectedFile] = useState<File | null>(null);
  const [progress, setProgress] = useState<number>(0);
  const [error, setError] = useState<string | null>(null);
  const [isUploading, setIsUploading] = useState(false);
  const [files, setFiles] = useState<FileManifestDto[]>([]);
  const [speedbps, setSpeedbps] = useState<number>(0);

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
    setProgress(0);
    setError(null);
  };

  const loadFiles = async () => {
    try {
      const list = await listFiles();
      setFiles(list);
    } catch {
      // ignore list errors here, show with alert if needed
    }
  };

  useEffect(() => {
    loadFiles();
  }, []);

  const onUpload = async () => {
    if (!selectedFile) return;
    if (!settings || !algo) {
      setError(
        t("files.noSettings", "Settings not loaded or unsupported algorithm."),
      );
      return;
    }
    setIsUploading(true);
    setError(null);
    const totalChunks = Math.ceil(
      selectedFile.size / settings.maxChunkSizeBytes,
    );
    const hashesLocal: string[] = new Array(totalChunks);
    let uploadedBytes = 0;
    const startTime = Date.now();
    setSpeedbps(0);
    try {
      // Prepare chunks with indexes for ordered hashes
      const chunks = Array.from(
        chunkBlob(selectedFile, settings.maxChunkSizeBytes),
      ).map((blob, idx) => ({ idx, blob, size: blob.size }));

      // Simple promise pool for concurrency
      const concurrency = UPLOAD_CONCURRENCY_DEFAULT;
      let next = 0;
      let active = 0;
      await new Promise<void>((resolve, reject) => {
        const runNext = () => {
          while (active < concurrency && next < chunks.length) {
            const current = chunks[next++];
            active++;
            (async () => {
              try {
                const h = await hashBlob(current.blob, algo);
                await uploadChunk(current.blob, h, selectedFile.name);
                hashesLocal[current.idx] = h;
                uploadedBytes += current.size;
                const elapsed = (Date.now() - startTime) / 1000;
                const bps = elapsed > 0 ? uploadedBytes / elapsed : 0;
                setSpeedbps(bps);
                const pct = Math.min(
                  100,
                  Math.round((uploadedBytes / selectedFile.size) * 100),
                );
                setProgress(pct);
              } catch (e) {
                reject(e);
                return;
              } finally {
                active--;
                if (next >= chunks.length && active === 0) {
                  resolve();
                } else {
                  runNext();
                }
              }
            })();
          }
        };
        runNext();
      });
      // Compute full file hash (over entire file blob) with same algorithm
      const fileHash = await hashBlob(selectedFile, algo);
      // Create file manifest on backend
      await createFileFromChunks({
        chunkHashes: hashesLocal,
        name: selectedFile.name,
        folder: "", // optional folder; adjust when folder selection UI appears
        contentType: selectedFile.type || "application/octet-stream",
        sha256: fileHash,
      });
      await loadFiles();
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
            {t(
              "files.settings",
              "Chunk size: {{size}} bytes, Algorithm: {{algo}}",
              {
                size: settings.maxChunkSizeBytes,
                algo: settings.supportedHashAlgorithm,
              },
            )}
          </Alert>
        )}
      </Box>
      <Stack
        direction={{ xs: "column", sm: "row" }}
        spacing={2}
        sx={{ mt: 2 }}
        alignItems="center"
      >
        <Button variant="outlined" component="label" disabled={isUploading}>
          {selectedFile
            ? t("files.changeFile", "Change file")
            : t("files.chooseFile", "Choose file")}
          <input hidden type="file" onChange={onFileChange} />
        </Button>
        <Typography variant="body2" sx={{ minWidth: 200 }}>
          {selectedFile
            ? selectedFile.name
            : t("files.noFile", "No file selected")}
        </Typography>
        <Button
          variant="contained"
          onClick={onUpload}
          disabled={!selectedFile || !settings || isUploading}
        >
          {isUploading
            ? t("files.uploading", "Uploading...")
            : t("files.upload", "Upload")}
        </Button>
      </Stack>
      {isUploading && (
        <Box sx={{ mt: 2 }}>
          <LinearProgress variant="determinate" value={progress} />
          <Stack direction="row" spacing={2} sx={{ mt: 0.5 }}>
            <Typography variant="caption">{progress}%</Typography>
            <Typography variant="caption">
              {t("files.threads", "Threads: {{count}}", {
                count: UPLOAD_CONCURRENCY_DEFAULT,
              })}
            </Typography>
            <Typography variant="caption">
              {t("files.speed", "Speed: {{speed}}/s", {
                speed: formatBytesPerSecond(speedbps),
              })}
            </Typography>
            <Typography variant="caption" color="text.secondary">
              {`${formatBytes(selectedFile?.size ?? 0)} total`}
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
        <Typography variant="h6">{t("files.filesGrid", "Files")}</Typography>
        <Box
          sx={{
            mt: 1,
            display: "grid",
            gridTemplateColumns: "repeat(auto-fill, minmax(160px, 1fr))",
            gap: 2,
          }}
        >
          {files.map((f) => (
            <Paper
              key={f.id}
              elevation={2}
              sx={{ p: 1.5, display: "flex", flexDirection: "column", gap: 1 }}
            >
              {/* Placeholder thumbnail */}
              <Box
                sx={{
                  width: "100%",
                  aspectRatio: "1 / 1",
                  bgcolor: "action.hover",
                  borderRadius: 1,
                  // https://png.pngtree.com/element_our/20190601/ourmid/pngtree-red-file-icon-free-illustration-image_1331449.jpg
                  backgroundImage: `url('https://images.freeimages.com/fic/images/icons/2813/flat_jewels/512/file.png')`,
                  backgroundSize: "contain",
                  backgroundRepeat: "no-repeat",
                  backgroundPosition: "center",
                }}
              />
              <Box>
                <Typography variant="body2" noWrap title={f.name}>
                  {f.name}
                </Typography>
                <Typography variant="caption" color="text.secondary">
                  {f.contentType}
                </Typography>
              </Box>
              <Box>
                <Link
                  href={getDownloadUrl(f.id)}
                  target="_blank"
                  rel="noopener noreferrer"
                >
                  {t("files.download", "Download")}
                </Link>
              </Box>
            </Paper>
          ))}
        </Box>
      </Box>
    </Box>
  );
};

export default FilesPage;
