import {
  Box,
  Menu,
  Link,
  Paper,
  Stack,
  Button,
  MenuItem,
  IconButton,
  Typography,
  Breadcrumbs,
  LinearProgress,
} from "@mui/material";
import {
  MoreVert,
  ArrowBack,
  CreateNewFolder,
  Home as HomeIcon,
  Folder as FolderIcon,
} from "@mui/icons-material";
import {
  hashBlob,
  chunkBlob,
  formatBytes,
  DEFAULT_CHUNK_SIZE,
  DEFAULT_CONCURRENCY,
  formatBytesPerSecond,
} from "../utils/fileUpload";
import { toast } from "react-toastify";
import { NodeType } from "../types/api";
import { useApi } from "../api/ApiContext";
import { fileIcon } from "../utils/fileIcons";
import { useTranslation } from "react-i18next";
import type { FunctionComponent } from "react";
import { DeleteOutline } from "@mui/icons-material";
import { useCallback, useEffect, useState, useRef } from "react";
import type { LayoutChildrenDto, LayoutNodeDto } from "../types/api";
import { useNavigate, useParams, useSearchParams } from "react-router-dom";

const FilesPage: FunctionComponent = () => {
  const { t } = useTranslation();
  const api = useApi();
  const navigate = useNavigate();
  const { nodeId } = useParams<{ nodeId?: string }>();
  const [searchParams] = useSearchParams();

  const typeParam = searchParams.get("type");
  const viewType: NodeType =
    typeParam === "trash" ? NodeType.Trash : NodeType.Default;

  const typeSuffix = viewType === NodeType.Trash ? "?type=trash" : "";

  // State
  const [loading, setLoading] = useState(false);
  const [currentNode, setCurrentNode] = useState<LayoutNodeDto | null>(null);
  const [children, setChildren] = useState<LayoutChildrenDto | null>(null);
  const [navStack, setNavStack] = useState<LayoutNodeDto[]>([]);
  const [pathNodes, setPathNodes] = useState<LayoutNodeDto[]>([]);
  const nodeCache = useState(() => new Map<string, LayoutNodeDto>())[0];
  const ancestorsCache = useRef(new Map<string, LayoutNodeDto[]>());
  const [selectedFile, setSelectedFile] = useState<File | null>(null);
  const [isUploading, setIsUploading] = useState(false);
  const [progressPct, setProgressPct] = useState(0);
  const [speedBps, setSpeedBps] = useState(0);
  const [uploadBytes, setUploadBytes] = useState(0);
  const [deletingFileId, setDeletingFileId] = useState<string | null>(null);
  const [deletingNodeId, setDeletingNodeId] = useState<string | null>(null);
  const [fileMenuAnchor, setFileMenuAnchor] = useState<{
    id: string;
    el: HTMLElement;
  } | null>(null);
  const [nodeMenuAnchor, setNodeMenuAnchor] = useState<{
    id: string;
    el: HTMLElement;
  } | null>(null);

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

  const abortUpload = useRef(false);

  const performUpload = useCallback(async () => {
    if (!selectedFile || !currentNode) return;
    abortUpload.current = false;
    setIsUploading(true);
    let uploadFailed = false;
    try {
      const start = Date.now();
      const chunks = Array.from(chunkBlob(selectedFile, DEFAULT_CHUNK_SIZE));
      const chunkHashes: string[] = new Array(chunks.length);
      let uploaded = 0;
      let next = 0;
      let active = 0;

      await new Promise<void>((resolve, reject) => {
        const runNext = () => {
          if (abortUpload.current) {
            reject(new Error("Upload aborted"));
            return;
          }
          while (active < DEFAULT_CONCURRENCY && next < chunks.length) {
            const index = next++;
            const blob = chunks[index];
            active++;
            (async () => {
              try {
                if (abortUpload.current) {
                  throw new Error("Upload aborted");
                }
                const h = await hashBlob(blob);
                if (abortUpload.current) {
                  throw new Error("Upload aborted");
                }
                chunkHashes[index] = h;
                const exists = await api.chunkExists(h);
                if (abortUpload.current) {
                  throw new Error("Upload aborted");
                }
                if (!exists) {
                  if (abortUpload.current) {
                    throw new Error("Upload aborted");
                  }
                  await api.uploadChunk(blob, h, selectedFile.name);
                  if (abortUpload.current) {
                    throw new Error("Upload aborted");
                  }
                }
                uploaded += blob.size;
                setUploadBytes(uploaded);
                const pct = Math.round((uploaded / selectedFile.size) * 100);
                setProgressPct(pct);
                const elapsed = (Date.now() - start) / 1000;
                if (elapsed > 0) setSpeedBps(uploaded / elapsed);
              } catch (err) {
                abortUpload.current = true;
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

      await api.createFileFromChunks({
        hash: "",
        chunkHashes,
        nodeId: currentNode.id,
        name: selectedFile.name,
        contentType: selectedFile.type,
      });
      const ch = await api.getNodeChildren(currentNode.id, viewType);
      setChildren(ch);
    } catch (e) {
      uploadFailed = true;
      const msg = e instanceof Error ? e.message : String(e);
      toast.error(msg);
    } finally {
      abortUpload.current = false;
      setIsUploading(false);
      setProgressPct(0);
      setSpeedBps(0);
      setUploadBytes(0);
      // Only clear file on successful upload
      if (!uploadFailed) {
        setSelectedFile(null);
      }
    }
  }, [selectedFile, currentNode, api, viewType]);

  const loadRoot = useCallback(async () => {
    try {
      setLoading(true);
      const root = await api.resolvePath(undefined, viewType);
      setCurrentNode(root);
      const ch = await api.getNodeChildren(root.id, viewType);
      setChildren(ch);
      setNavStack([]);
      nodeCache.set(root.id, root);
      setPathNodes([root]);
      // Ensure URL shows the root node id
      navigate(`/files/${root.id}${typeSuffix}`, { replace: true });
    } catch (e) {
      const msg = e instanceof Error ? e.message : String(e);
      toast.error(msg);
    } finally {
      setLoading(false);
    }
  }, [nodeCache, api, navigate, viewType, typeSuffix]);

  const openNode = useCallback(
    async (node: LayoutNodeDto) => {
      // Navigate to URL for the node; effect will load contents
      navigate(`/files/${node.id}${typeSuffix}`);
    },
    [navigate, typeSuffix],
  );

  const goBack = useCallback(async () => {
    if (viewType === NodeType.Trash) {
      navigate(`/files`);
      return;
    }
    if (navStack.length === 0) {
      await loadRoot();
      return;
    }
    const next = [...navStack];
    const prev = next.pop()!;
    setNavStack(next);
    await openNode(prev);
  }, [navStack, loadRoot, openNode, navigate, viewType]);

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
      const ch = await api.getNodeChildren(currentNode.id, viewType);
      setChildren(ch);
    } catch (e) {
      const msg = e instanceof Error ? e.message : String(e);
      toast.error(msg);
    } finally {
      setLoading(false);
    }
  }, [currentNode, t, api, viewType]);

  const onDeleteFile = useCallback(
    async (fileId: string) => {
      if (!currentNode) return;
      try {
        setDeletingFileId(fileId);
        await api.deleteFile(fileId);
        const ch = await api.getNodeChildren(currentNode.id, viewType);
        setChildren(ch);
      } catch (e) {
        const msg = e instanceof Error ? e.message : String(e);
        toast.error(msg);
      } finally {
        setDeletingFileId(null);
        setFileMenuAnchor(null);
      }
    },
    [api, currentNode, viewType],
  );

  const onDeleteNode = useCallback(
    async (nodeId: string) => {
      if (!currentNode) return;
      try {
        setDeletingNodeId(nodeId);
        await api.deleteNode(nodeId);
        const ch = await api.getNodeChildren(currentNode.id, viewType);
        setChildren(ch);
      } catch (e) {
        const msg = e instanceof Error ? e.message : String(e);
        toast.error(msg);
      } finally {
        setDeletingNodeId(null);
        setNodeMenuAnchor(null);
      }
    },
    [api, currentNode, viewType],
  );

  // Sync with route param: load specific node or redirect to root
  useEffect(() => {
    (async () => {
      if (nodeId) {
        try {
          setLoading(true);
          const node = await api.getNode(nodeId);
          setCurrentNode(node);
          nodeCache.set(node.id, node);
          const ch = await api.getNodeChildren(node.id, viewType);
          setChildren(ch);
        } catch (e) {
          const msg = e instanceof Error ? e.message : String(e);
          toast.error(msg);
        } finally {
          setLoading(false);
        }
      } else {
        await loadRoot();
      }
    })();
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [nodeId, viewType]);

  useEffect(() => {
    let mounted = true;
    (async () => {
      if (!currentNode) {
        if (mounted) setPathNodes([]);
        return;
      }
      if (!currentNode.parentId) {
        if (mounted) setPathNodes([currentNode]);
        return;
      }

      // Check cache first
      const cacheKey = `${currentNode.id}-${viewType}`;
      const cached = ancestorsCache.current.get(cacheKey);
      if (cached) {
        let path = cached;
        if (!path.length || path[path.length - 1].id !== currentNode.id) {
          path = [...path, currentNode];
        }
        if (mounted) setPathNodes(path);
        return;
      }

      // Fetch and cache
      let path = await api.getAncestors(currentNode.id, viewType);
      ancestorsCache.current.set(cacheKey, path);
      if (!path.length || path[path.length - 1].id !== currentNode.id) {
        path = [...path, currentNode];
      }
      if (mounted) setPathNodes(path);
    })();
    return () => {
      mounted = false;
    };
  }, [currentNode, api, viewType]);

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
              const isRoot = !n.parentId;
              if (isRoot) {
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
            title={t("filesPage.trash")}
            onClick={() => {
              if (viewType === NodeType.Trash) {
                navigate(`/files`);
              } else {
                navigate(`/files${`?type=trash`}`);
              }
            }}
            color={viewType === NodeType.Trash ? "error" : "default"}
          >
            <DeleteOutline />
          </IconButton>
          <IconButton
            title={t("filesPage.newFolder")}
            onClick={onCreateFolder}
            disabled={loading || !currentNode || viewType === NodeType.Trash}
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
        {children &&
        children.nodes.length === 0 &&
        children.files.length === 0 ? (
          <Box sx={{ mt: 3, textAlign: "center", color: "text.secondary" }}>
            <Typography variant="body2">
              {t("filesPage.emptyFolder", "No files")}
            </Typography>
          </Box>
        ) : (
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
                onClick={() => {
                  if (nodeMenuAnchor || fileMenuAnchor) return;
                  openNode(n);
                }}
              >
                <IconButton
                  size="small"
                  sx={{ position: "absolute", top: 12, right: 12 }}
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
                    borderRadius: 1,
                    display: "flex",
                    alignItems: "center",
                    justifyContent: "center",
                  }}
                >
                  <FolderIcon sx={{ fontSize: 120 }} color="warning" />
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
                  <MenuItem
                    onClick={() => onDeleteNode(n.id)}
                    disabled={deletingNodeId === n.id}
                  >
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
                sx={{
                  p: 1.5,
                  display: "flex",
                  flexDirection: "column",
                  gap: 1,
                  position: "relative",
                  cursor: "pointer",
                  opacity: deletingFileId === f.id ? 0.6 : 1,
                  pointerEvents: deletingFileId === f.id ? "none" : "auto",
                }}
                onClick={() => {
                  if (fileMenuAnchor) return;
                  const url = api.getDownloadUrl(f.id);
                  window.open(url, "_blank", "noopener,noreferrer");
                }}
              >
                <IconButton
                  size="small"
                  sx={{ position: "absolute", top: 12, right: 12 }}
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
                    borderRadius: 1,
                    display: "flex",
                    alignItems: "center",
                    justifyContent: "center",
                    "& > svg": { fontSize: 120 },
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
                <Menu
                  open={fileMenuAnchor?.id === f.id}
                  anchorEl={fileMenuAnchor?.el}
                  onClose={() => setFileMenuAnchor(null)}
                  anchorOrigin={{ vertical: "bottom", horizontal: "right" }}
                  transformOrigin={{ vertical: "top", horizontal: "right" }}
                >
                  <MenuItem
                    onClick={() => onDeleteFile(f.id)}
                    disabled={deletingFileId === f.id}
                  >
                    {t("filesPage.deleteFile", "Delete file")}
                  </MenuItem>
                </Menu>
              </Paper>
            ))}
          </Box>
        )}
      </Box>
    </Box>
  );
};

export default FilesPage;
