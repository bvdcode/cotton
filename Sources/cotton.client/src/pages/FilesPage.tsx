import {
  Box,
  Link,
  Alert,
  Paper,
  Stack,
  Button,
  IconButton,
  Typography,
  Breadcrumbs,
  LinearProgress,
  Menu,
  MenuItem,
} from "@mui/material";
import {
  ArrowBack,
  CreateNewFolder,
  Home as HomeIcon,
  Folder as FolderIcon,
  MoreVert,
} from "@mui/icons-material";
import {
  hashBlob,
  hashFile,
  chunkBlob,
  formatBytes,
  DEFAULT_CHUNK_SIZE,
  DEFAULT_CONCURRENCY,
  formatBytesPerSecond,
} from "../utils/fileUpload";
import { fileIcon } from "../utils/fileIcons";
import { useApi } from "../api/ApiContext";
import { useTranslation } from "react-i18next";
import type { FunctionComponent } from "react";
import { useCallback, useEffect, useState } from "react";
import type { LayoutChildrenDto, LayoutNodeDto } from "../types/api";

const FilesPage: FunctionComponent = () => {
  const { t } = useTranslation();
  const api = useApi();

  // State
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [currentNode, setCurrentNode] = useState<LayoutNodeDto | null>(null);
  const [children, setChildren] = useState<LayoutChildrenDto | null>(null);
  const [navStack, setNavStack] = useState<LayoutNodeDto[]>([]);
  const [pathNodes, setPathNodes] = useState<LayoutNodeDto[]>([]);
  const nodeCache = useState(() => new Map<string, LayoutNodeDto>())[0];
  const [selectedFile, setSelectedFile] = useState<File | null>(null);
  const [isUploading, setIsUploading] = useState(false);
  const [progressPct, setProgressPct] = useState(0);
  const [speedBps, setSpeedBps] = useState(0);
  const [uploadBytes, setUploadBytes] = useState(0);
  const [deletingFileId, setDeletingFileId] = useState<string | null>(null);
  const [deletingNodeId, setDeletingNodeId] = useState<string | null>(null);
  const [fileMenuAnchor, setFileMenuAnchor] = useState<{ id: string; el: HTMLElement } | null>(null);
  const [nodeMenuAnchor, setNodeMenuAnchor] = useState<{ id: string; el: HTMLElement } | null>(null);

  const prettyFileType = (name: string, contentType?: string): string => {
    const ext = name.includes(".") ? name.split(".").pop()!.toUpperCase() : "";
    const ct = contentType || "";
    if (ct.startsWith("image/")) return "Image";
    if (ct.startsWith("video/")) return "Video";
    if (ct.startsWith("audio/")) return "Audio";
    if (ct === "application/pdf") return "PDF";
    if (ct === "application/zip") return "ZIP";
    if (ct === "application/vnd.android.package-archive") return "APK";
    if (ct === "text/plain") return "Text";
    if (ct === "application/json") return "JSON";
    if (ext && ext.length <= 5) return ext;
    return ct || "File";
  };
  const onFileChange = (e: React.ChangeEvent<HTMLInputElement>) => {
    const f = e.target.files?.[0] ?? null;
    setSelectedFile(f);
    setProgressPct(0);
    setSpeedBps(0);
    setUploadBytes(0);
  };

  const performUpload = useCallback(async () => {
    if (!selectedFile || !currentNode) return;
    setIsUploading(true);
    setError(null);
    try {
      const start = Date.now();
      const chunks = Array.from(chunkBlob(selectedFile, DEFAULT_CHUNK_SIZE));
      const chunkHashes: string[] = new Array(chunks.length);
      let uploaded = 0;
      let next = 0;
      let active = 0;

      await new Promise<void>((resolve, reject) => {
        const runNext = () => {
          while (active < DEFAULT_CONCURRENCY && next < chunks.length) {
            const index = next++;
            const blob = chunks[index];
            active++;
            (async () => {
              try {
                const h = await hashBlob(blob);
                chunkHashes[index] = h;
                const exists = await api.chunkExists(h);
                if (!exists) {
                  await api.uploadChunk(blob, h, selectedFile.name);
                }
                uploaded += blob.size;
                setUploadBytes(uploaded);
                const pct = Math.round((uploaded / selectedFile.size) * 100);
                setProgressPct(pct);
                const elapsed = (Date.now() - start) / 1000;
                if (elapsed > 0) setSpeedBps(uploaded / elapsed);
              } catch (err) {
                reject(err);
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

      const fileHash = await hashFile(selectedFile);
      await api.createFileFromChunks({
        chunkHashes,
        name: selectedFile.name,
        contentType: selectedFile.type || "application/octet-stream",
        hash: fileHash,
        nodeId: currentNode.id,
      });
      const ch = await api.getNodeChildren(currentNode.id);
      setChildren(ch);
    } catch (e) {
      const msg = e instanceof Error ? e.message : String(e);
      setError(msg);
    } finally {
      setIsUploading(false);
      setProgressPct(0);
      setSpeedBps(0);
      setUploadBytes(0);
      setSelectedFile(null);
    }
  }, [selectedFile, currentNode, api]);

  const loadRoot = useCallback(async () => {
    try {
      setLoading(true);
      setError(null);
      const root = await api.resolvePath();
      setCurrentNode(root);
      const ch = await api.getNodeChildren(root.id);
      setChildren(ch);
      setNavStack([]);
      nodeCache.set(root.id, root);
      setPathNodes([root]);
    } catch (e) {
      const msg = e instanceof Error ? e.message : String(e);
      setError(msg);
    } finally {
      setLoading(false);
    }
  }, [nodeCache, api]);

  const openNode = useCallback(
    async (node: LayoutNodeDto) => {
      try {
        setLoading(true);
        setError(null);
        setNavStack((s) => (currentNode ? [...s, currentNode] : s));
        setCurrentNode(node);
        nodeCache.set(node.id, node);
        const ch = await api.getNodeChildren(node.id);
        setChildren(ch);
      } catch (e) {
        const msg = e instanceof Error ? e.message : String(e);
        setError(msg);
      } finally {
        setLoading(false);
      }
    },
    [currentNode, nodeCache, api],
  );

  const goBack = useCallback(async () => {
    if (navStack.length === 0) {
      await loadRoot();
      return;
    }
    const next = [...navStack];
    const prev = next.pop()!;
    setNavStack(next);
    await openNode(prev);
  }, [navStack, loadRoot, openNode]);

  const onCreateFolder = useCallback(async () => {
    if (!currentNode?.id) return;
    const name = window.prompt(
      t("filesPage.enterFolderName", "Enter folder name"),
      "",
    );
    if (!name) return;
    const trimmed = name.trim();
    if (!trimmed) return;
    try {
      setLoading(true);
      await api.createFolder({ parentId: currentNode.id, name: trimmed });
      const ch = await api.getNodeChildren(currentNode.id);
      setChildren(ch);
    } catch (e) {
      const msg = e instanceof Error ? e.message : String(e);
      setError(msg);
    } finally {
      setLoading(false);
    }
  }, [currentNode, t, api]);

  const onDeleteFile = useCallback(async (fileId: string) => {
    if (!currentNode) return;
    try {
      setDeletingFileId(fileId);
      await api.deleteFile(fileId);
      const ch = await api.getNodeChildren(currentNode.id);
      setChildren(ch);
    } catch (e) {
      const msg = e instanceof Error ? e.message : String(e);
      setError(msg);
    } finally {
      setDeletingFileId(null);
      setFileMenuAnchor(null);
    }
  }, [api, currentNode]);

  const onDeleteNode = useCallback(async (nodeId: string) => {
    if (!currentNode) return;
    try {
      setDeletingNodeId(nodeId);
      await api.deleteNode(nodeId);
      const ch = await api.getNodeChildren(currentNode.id);
      setChildren(ch);
    } catch (e) {
      const msg = e instanceof Error ? e.message : String(e);
      setError(msg);
    } finally {
      setDeletingNodeId(null);
      setNodeMenuAnchor(null);
    }
  }, [api, currentNode]);

  useEffect(() => {
    loadRoot();
  }, [loadRoot]);

  // Rebuild breadcrumbs when currentNode changes
  useEffect(() => {
    const buildPath = async () => {
      if (!currentNode) {
        setPathNodes([]);
        return;
      }
      const acc: LayoutNodeDto[] = [];
      let cur: LayoutNodeDto | undefined = currentNode;
      const guard = new Set<string>();
      while (cur && !guard.has(cur.id)) {
        acc.unshift(cur);
        guard.add(cur.id);
        const pid = cur.parentId ?? null;
        if (!pid) break;
        const cached = nodeCache.get(pid);
        if (cached) {
          cur = cached;
        } else {
          try {
            const parent = await api.getNode(pid);
            nodeCache.set(parent.id, parent);
            cur = parent;
          } catch {
            break;
          }
        }
      }
      setPathNodes(acc);
    };
    buildPath();
  }, [currentNode, nodeCache, api]);

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
            {pathNodes.map((n, idx) => {
              const isLast = idx === pathNodes.length - 1;
              if (idx === 0) {
                return isLast ? (
                  <Typography
                    key={n.id}
                    color="text.primary"
                    variant="body2"
                    title={n.name}
                  >
                    <HomeIcon
                      fontSize="small"
                      style={{ verticalAlign: "middle" }}
                    />
                  </Typography>
                ) : (
                  <Link
                    key={n.id}
                    underline="none"
                    color="inherit"
                    sx={{
                      display: "inline-flex",
                      alignItems: "center",
                      cursor: "pointer",
                    }}
                    onClick={() => openNode(n)}
                    title={n.name}
                  >
                    <HomeIcon fontSize="small" />
                  </Link>
                );
              }
              return isLast ? (
                <Typography key={n.id} color="text.primary" title={n.name}>
                  {n.name}
                </Typography>
              ) : (
                <Link
                  key={n.id}
                  underline="hover"
                  color="inherit"
                  onClick={() => openNode(n)}
                  title={n.name}
                  sx={{ cursor: "pointer" }}
                >
                  {n.name}
                </Link>
              );
            })}
          </Breadcrumbs>
        </Box>

        {loading && <LinearProgress />}
        {error && <Alert severity="error">{error}</Alert>}

        <Stack
          direction={{ xs: "column", sm: "row" }}
          spacing={2}
          sx={{ mt: 2 }}
          alignItems="center"
        >
          <Typography variant="body2">
            {selectedFile?.name ?? t("filesPage.noFile")}
          </Typography>
          <Button
            variant="outlined"
            component="label"
            disabled={isUploading || loading}
          >
            {selectedFile
              ? t("filesPage.changeFile")
              : t("filesPage.chooseFile")}
            <input hidden type="file" onChange={onFileChange} />
          </Button>
          <Button
            variant="contained"
            disabled={!selectedFile || isUploading || loading || !currentNode}
            onClick={performUpload}
          >
            {isUploading ? t("filesPage.uploading") : t("filesPage.upload")}
          </Button>
          <IconButton
            title={t("filesPage.back")}
            onClick={goBack}
            disabled={loading}
          >
            <ArrowBack />
          </IconButton>
          <IconButton
            title={t("filesPage.newFolder")}
            onClick={onCreateFolder}
            disabled={loading || !currentNode}
          >
            <CreateNewFolder />
          </IconButton>
        </Stack>
      </Box>

      <Box sx={{ mt: 4 }}>
        {isUploading && (
          <Box sx={{ mb: 2 }}>
            <LinearProgress variant="determinate" value={progressPct} />
            <Stack direction="row" spacing={2} sx={{ mt: 0.5 }}>
              <Typography variant="caption">{progressPct}%</Typography>
              <Typography variant="caption">
                {t("filesPage.threads", { count: DEFAULT_CONCURRENCY })}
              </Typography>
              <Typography variant="caption">
                {t("filesPage.speed", {
                  speed: formatBytesPerSecond(speedBps),
                })}
              </Typography>
              <Typography
                variant="caption"
                color="text.secondary"
              >{`${formatBytes(uploadBytes)} / ${formatBytes(
                selectedFile?.size ?? 0,
              )}`}</Typography>
            </Stack>
          </Box>
        )}
        <Box
          sx={{
            mt: 1,
            display: "grid",
            gridTemplateColumns: "repeat(auto-fill, minmax(160px, 1fr))",
            gap: 2,
          }}
        >
          {/* Folders */}
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
                position: "relative",
                opacity: deletingNodeId === n.id ? 0.6 : 1,
                pointerEvents: deletingNodeId === n.id ? "none" : "auto",
              }}
              onClick={() => openNode(n)}
            >
              <IconButton
                size="small"
                sx={{ position: "absolute", top: 4, right: 4 }}
                onClick={(e) => {
                  e.stopPropagation();
                  setNodeMenuAnchor({ id: n.id, el: e.currentTarget });
                }}
              >
                <MoreVert fontSize="small" />
              </IconButton>
              <Box
                sx={{
                  width: "100%",
                  aspectRatio: "1 / 1",
                  bgcolor: "action.hover",
                  borderRadius: 1,
                  display: "flex",
                  alignItems: "center",
                  justifyContent: "center",
                }}
              >
                <FolderIcon fontSize="large" color="warning" />
              </Box>
              <Box>
                <Typography variant="body2" noWrap title={n.name}>
                  {n.name}
                </Typography>
                <Typography variant="caption" color="text.secondary">
                  {t("filesPage.folder")}
                </Typography>
              </Box>
              <Menu
                open={nodeMenuAnchor?.id === n.id}
                anchorEl={nodeMenuAnchor?.el}
                onClose={() => setNodeMenuAnchor(null)}
                anchorOrigin={{ vertical: "bottom", horizontal: "right" }}
                transformOrigin={{ vertical: "top", horizontal: "right" }}
              >
                <MenuItem onClick={() => onDeleteNode(n.id)} disabled={deletingNodeId === n.id}>
                  {t("filesPage.deleteFolder", "Delete folder")}
                </MenuItem>
              </Menu>
            </Paper>
          ))}

          {/* Files */}
          {children?.files.map((f) => (
            <Paper
              key={f.id}
              elevation={2}
              sx={{ p: 1.5, display: "flex", flexDirection: "column", gap: 1, position: "relative", opacity: deletingFileId === f.id ? 0.6 : 1, pointerEvents: deletingFileId === f.id ? "none" : "auto" }}
            >
              <IconButton
                size="small"
                sx={{ position: "absolute", top: 4, right: 4 }}
                onClick={(e) => {
                  e.stopPropagation();
                  setFileMenuAnchor({ id: f.id, el: e.currentTarget });
                }}
              >
                <MoreVert fontSize="small" />
              </IconButton>
              <Box
                sx={{
                  width: "100%",
                  aspectRatio: "1 / 1",
                  bgcolor: "action.hover",
                  borderRadius: 1,
                  display: "flex",
                  alignItems: "center",
                  justifyContent: "center",
                }}
              >
                {fileIcon(f.name, f.contentType)}
              </Box>
              <Box>
                <Typography variant="body2" noWrap title={f.name}>
                  {f.name}
                </Typography>
                <Typography
                  variant="caption"
                  color="text.secondary"
                  noWrap
                  title={f.contentType}
                >
                  {prettyFileType(f.name, f.contentType)}
                </Typography>
              </Box>
              <Box>
                <Link
                  href={api.getDownloadUrl(f.id)}
                  target="_blank"
                  rel="noopener noreferrer"
                >
                  {t("filesPage.download")}
                </Link>
              </Box>
              <Menu
                open={fileMenuAnchor?.id === f.id}
                anchorEl={fileMenuAnchor?.el}
                onClose={() => setFileMenuAnchor(null)}
                anchorOrigin={{ vertical: "bottom", horizontal: "right" }}
                transformOrigin={{ vertical: "top", horizontal: "right" }}
              >
                <MenuItem onClick={() => onDeleteFile(f.id)} disabled={deletingFileId === f.id}>
                  {t("filesPage.deleteFile", "Delete file")}
                </MenuItem>
              </Menu>
            </Paper>
          ))}
        </Box>
      </Box>
    </Box>
  );
};

export default FilesPage;
