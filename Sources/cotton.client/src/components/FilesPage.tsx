import {
  uploadChunk,
  createFileFromChunks,
  getDownloadUrl,
} from "../api/files.ts";
import Box from "@mui/material/Box";
import Link from "@mui/material/Link";
import Paper from "@mui/material/Paper";
import Stack from "@mui/material/Stack";
import Alert from "@mui/material/Alert";
import Button from "@mui/material/Button";
import { chunkBlob } from "../utils/chunk.ts";
import { useTranslation } from "react-i18next";
import Typography from "@mui/material/Typography";
import { useState, useMemo, useEffect } from "react";
import { useSettings } from "../stores/settingsStore.ts";
import LinearProgress from "@mui/material/LinearProgress";
import { useLayoutStore } from "../stores/layoutStore.ts";
import { useNavigate, useParams } from "react-router-dom";
import { UPLOAD_CONCURRENCY_DEFAULT } from "../config.ts";
import { normalizeAlgorithm, hashBlob } from "../utils/hash.ts";
import { formatBytes, formatBytesPerSecond } from "../utils/format";
import { CreateNewFolder } from "@mui/icons-material";
import { IconButton } from "@mui/material";
import { createFolder } from "../api/layout.ts";

const FilesPage = () => {
  const {
    currentNode,
    children,
    loading: layoutLoading,
    error: layoutError,
    resolveRoot,
    loadChildren,
    openNodeById,
  } = useLayoutStore();
  const { t } = useTranslation();
  const navigate = useNavigate();
  const { nodeId } = useParams();
  const settings = useSettings((s) => s.settings);
  const errorSettings = useSettings((s) => s.error);
  const [progress, setProgress] = useState<number>(0);
  const [speedbps, setSpeedbps] = useState<number>(0);
  const [isUploading, setIsUploading] = useState(false);
  const loadingSettings = useSettings((s) => s.loading);
  const [error, setError] = useState<string | null>(null);
  const [selectedFile, setSelectedFile] = useState<File | null>(null);

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

  useEffect(() => {
    // If route has nodeId param, open it; otherwise resolve root and navigate to it
    (async () => {
      if (nodeId) {
        await openNodeById(nodeId);
      } else {
        await resolveRoot();
        if (currentNode?.id) {
          navigate(`/app/files/${currentNode.id}`, { replace: true });
        }
      }
    })();
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [nodeId]);

  const onUpload = async () => {
    if (!selectedFile) return;
    if (!settings || !algo) {
      setError(
        t("files.noSettings", "Settings not loaded or unsupported algorithm."),
      );
      return;
    }
    // currentNode is resolved on mount; no manual folder selection required
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
        contentType: selectedFile.type || "application/octet-stream",
        sha256: fileHash,
        nodeId: currentNode!.id,
      });
      // refresh current node children after upload
      await loadChildren(currentNode?.id);
    } catch (_e) {
      const msg = _e instanceof Error ? _e.message : String(_e);
      setError(msg);
    } finally {
      setIsUploading(false);
    }
  };

  return (
    <Box>
      <Box sx={{ mt: 2, display: "flex", justifyContent: "space-between" }}>
        <Typography variant="h4" gutterBottom>
          {t("files.title", "Files")}
        </Typography>
        {(loadingSettings || layoutLoading) && <LinearProgress />}
        {(errorSettings || layoutError) && (
          <Alert severity="error">{errorSettings ?? layoutError}</Alert>
        )}
        <Stack
          direction={{ xs: "column", sm: "row" }}
          spacing={2}
          sx={{ mt: 2 }}
          alignItems="center"
        >
          <Typography variant="body2">
            {selectedFile ? selectedFile.name : t("files.noFile")}
          </Typography>
          <Button variant="outlined" component="label" disabled={isUploading}>
            {selectedFile ? t("files.changeFile") : t("files.chooseFile")}
            <input hidden type="file" onChange={onFileChange} />
          </Button>
          <Button
            variant="contained"
            onClick={onUpload}
            disabled={!selectedFile || !settings || isUploading || !currentNode}
          >
            {isUploading ? t("files.uploading") : t("files.upload")}
          </Button>
          <IconButton
            title={t("files.newFolder", "New folder")}
            onClick={async () => {
              if (!currentNode?.id) return;
              const name = window.prompt(
                t("files.enterFolderName", "Enter folder name"),
                "",
              );
              if (!name) return;
              const trimmed = name.trim();
              if (!trimmed) return;
              try {
                await createFolder({ parentId: currentNode.id, name: trimmed });
                await loadChildren(currentNode.id);
              } catch (e) {
                const msg = e instanceof Error ? e.message : String(e);
                setError(msg);
              }
            }}
            disabled={!currentNode}
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
              {t("files.threads", { count: UPLOAD_CONCURRENCY_DEFAULT })}
            </Typography>
            <Typography variant="caption">
              {t("files.speed", { speed: formatBytesPerSecond(speedbps) })}
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
        <Typography variant="h6">{t("files.filesGrid")}</Typography>
        <Box
          sx={{
            mt: 1,
            display: "grid",
            gridTemplateColumns: "repeat(auto-fill, minmax(160px, 1fr))",
            gap: 2,
          }}
        >
          {/* Folders (nodes) */}
          {children?.nodes.map((n) => (
            <Paper
              key={n.id}
              elevation={2}
              sx={{
                p: 1.5,
                display: "flex",
                flexDirection: "column",
                gap: 1,
                cursor: "pointer",
              }}
              onClick={() => navigate(`/app/files/${n.id}`)}
            >
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
                <Typography variant="body2" noWrap title={n.name}>
                  {n.name}
                </Typography>
                <Typography variant="caption" color="text.secondary">
                  Folder
                </Typography>
              </Box>
            </Paper>
          ))}

          {/* Files */}
          {children?.files.map((f) => (
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
                  {t("files.download")}
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
