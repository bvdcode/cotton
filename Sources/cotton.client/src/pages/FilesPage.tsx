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
import { useCallback, useEffect, useState } from "react";
import { filesApi, layoutApi } from "../api";
import type { LayoutChildrenDto, LayoutNodeDto } from "../types/api";

const FilesPage: FunctionComponent = () => {
  const { t } = useTranslation();

  // State
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [currentNode, setCurrentNode] = useState<LayoutNodeDto | null>(null);
  const [children, setChildren] = useState<LayoutChildrenDto | null>(null);
  const [navStack, setNavStack] = useState<LayoutNodeDto[]>([]);

  const loadRoot = useCallback(async () => {
    try {
      setLoading(true);
      setError(null);
      const root = await layoutApi.resolvePath();
      setCurrentNode(root);
      const ch = await layoutApi.getNodeChildren(root.id);
      setChildren(ch);
      setNavStack([]);
    } catch (e) {
      const msg = e instanceof Error ? e.message : String(e);
      setError(msg);
    } finally {
      setLoading(false);
    }
  }, []);

  const openNode = useCallback(async (node: LayoutNodeDto) => {
    try {
      setLoading(true);
      setError(null);
      setNavStack((s) => (currentNode ? [...s, currentNode] : s));
      setCurrentNode(node);
      const ch = await layoutApi.getNodeChildren(node.id);
      setChildren(ch);
    } catch (e) {
      const msg = e instanceof Error ? e.message : String(e);
      setError(msg);
    } finally {
      setLoading(false);
    }
  }, [currentNode]);

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
    const name = window.prompt(t("filesPage.enterFolderName", "Enter folder name"), "");
    if (!name) return;
    const trimmed = name.trim();
    if (!trimmed) return;
    try {
      setLoading(true);
      await layoutApi.createFolder({ parentId: currentNode.id, name: trimmed });
      const ch = await layoutApi.getNodeChildren(currentNode.id);
      setChildren(ch);
    } catch (e) {
      const msg = e instanceof Error ? e.message : String(e);
      setError(msg);
    } finally {
      setLoading(false);
    }
  }, [currentNode, t]);

  useEffect(() => {
    loadRoot();
  }, [loadRoot]);

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

  {loading && <LinearProgress />}
  {error && <Alert severity="error">{error}</Alert>}

        <Stack
          direction={{ xs: "column", sm: "row" }}
          spacing={2}
          sx={{ mt: 2 }}
          alignItems="center"
        >
          <Typography variant="body2">{currentNode?.name ?? t("filesPage.noFile")}</Typography>
          <Button variant="outlined" component="label" disabled>
            {t("filesPage.chooseFile")}
            <input hidden type="file" />
          </Button>
          <Button variant="contained" disabled>
            {t("filesPage.upload")}
          </Button>
          <IconButton title={t("filesPage.back")} onClick={goBack} disabled={loading}>
            <ArrowBack />
          </IconButton>
          <IconButton title={t("filesPage.newFolder")} onClick={onCreateFolder} disabled={loading || !currentNode}>
            <CreateNewFolder />
          </IconButton>
        </Stack>
      </Box>

      <Box sx={{ mt: 4 }}>
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
            <Paper key={n.id} elevation={2} sx={{ p: 1.5, display: "flex", flexDirection: "column", gap: 1, cursor: "pointer" }}
              onClick={() => openNode(n)}>
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
                <Typography variant="body2" noWrap title={n.name}>{n.name}</Typography>
                <Typography variant="caption" color="text.secondary">
                  {t("filesPage.folder")}
                </Typography>
              </Box>
            </Paper>
          ))}

          {/* Files */}
          {children?.files.map((f) => (
            <Paper key={f.id} elevation={2} sx={{ p: 1.5, display: "flex", flexDirection: "column", gap: 1 }}>
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
                <Typography variant="body2" noWrap title={f.name}>{f.name}</Typography>
                <Typography variant="caption" color="text.secondary">{f.contentType}</Typography>
              </Box>
              <Box>
                <Link href={filesApi.getDownloadUrl(f.id)} target="_blank" rel="noopener noreferrer">
                  {t("filesPage.download")}
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
