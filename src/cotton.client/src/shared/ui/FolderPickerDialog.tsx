import { ArrowUpward, Folder, Home } from "@mui/icons-material";
import {
  Alert,
  Button,
  CircularProgress,
  Dialog,
  DialogActions,
  DialogContent,
  DialogTitle,
  IconButton,
  List,
  ListItemButton,
  ListItemIcon,
  ListItemText,
  Stack,
  Tooltip,
  Typography,
  useMediaQuery,
} from "@mui/material";
import { useQuery } from "@tanstack/react-query";
import { useState } from "react";
import { useTranslation } from "react-i18next";
import { layoutsApi, type NodeDto } from "../api/layoutsApi";
import { getFolderChoices } from "../api/folderChoicesApi";
import {
  useDecryptedNode,
  useDecryptedNodes,
} from "../crypto/useDecryptedNodes";
import { useAuthStore } from "../store/authStore";

interface Props {
  initialNodeId?: string;
  onSelect: (folder: NodeDto) => void;
  onClose: () => void;
}
export const FolderPickerDialog = ({
  initialNodeId,
  onSelect,
  onClose,
}: Props) => {
  const { t } = useTranslation("routines");
  const userId = useAuthStore((state) => state.user?.id);
  const mobile = useMediaQuery((theme) => theme.breakpoints.down("sm"));
  const [open, setOpen] = useState(true);
  const [nodeId, setNodeId] = useState(initialNodeId);
  const [page, setPage] = useState(1);
  const root = useQuery({
    queryKey: ["folderPickerRoot", userId],
    queryFn: () => layoutsApi.resolve(),
    enabled: open,
  });
  const currentId = nodeId ?? root.data?.id;
  const folders = useQuery({
    queryKey: ["folderChoices", userId, currentId, page],
    queryFn: ({ signal }) => getFolderChoices(currentId!, page, signal),
    enabled: open && !!currentId,
  });
  const current = useDecryptedNode(folders.data?.current ?? null);
  const choices = useDecryptedNodes(folders.data?.folders ?? []);
  const navigate = (id: string) => {
    setNodeId(id);
    setPage(1);
  };
  return (
    <Dialog
      open={open}
      onClose={() => setOpen(false)}
      maxWidth="sm"
      fullWidth
      fullScreen={mobile}
      slotProps={{ paper: { elevation: 8 }, transition: { onExited: onClose } }}
    >
      <DialogTitle>{t("chooseFolder")}</DialogTitle>
      <DialogContent dividers>
        <Stack spacing={2}>
          <Stack direction="row" spacing={1} sx={{ alignItems: "center" }}>
            <Tooltip title={t("home")}>
              <span>
                <IconButton
                  aria-label={t("home")}
                  disabled={!root.data}
                  onClick={() => {
                    if (root.data) {
                      navigate(root.data.id);
                    }
                  }}
                >
                  <Home />
                </IconButton>
              </span>
            </Tooltip>
            <Tooltip title={t("up")}>
              <span>
                <IconButton
                  aria-label={t("up")}
                  disabled={!current?.parentId}
                  onClick={() => {
                    if (current?.parentId) {
                      navigate(current.parentId);
                    }
                  }}
                >
                  <ArrowUpward />
                </IconButton>
              </span>
            </Tooltip>
            <Typography sx={{ overflowWrap: "anywhere" }}>
              {current?.parentId ? current.name : t("home")}
            </Typography>
          </Stack>
          {(root.isError || folders.isError) && (
            <Alert severity="error">{t("loadError")}</Alert>
          )}
          <Stack
            sx={{ minHeight: "16rem", maxHeight: "28rem", overflow: "auto" }}
          >
            {folders.isPending && (
              <Stack
                sx={{ flex: 1, alignItems: "center", justifyContent: "center" }}
              >
                <CircularProgress />
              </Stack>
            )}
            <List disablePadding>
              {choices.map((folder) => (
                <ListItemButton
                  key={folder.id}
                  onClick={() => navigate(folder.id)}
                >
                  <ListItemIcon>
                    <Folder />
                  </ListItemIcon>
                  <ListItemText
                    primary={folder.name}
                    slotProps={{
                      primary: { sx: { overflowWrap: "anywhere" } },
                    }}
                  />
                </ListItemButton>
              ))}
            </List>
            {folders.isSuccess && choices.length === 0 && (
              <Typography
                color="text.secondary"
                sx={{ my: "auto", textAlign: "center" }}
              >
                {t("noSubfolders")}
              </Typography>
            )}
          </Stack>
          {(page > 1 || folders.data?.hasMore) && (
            <Stack direction="row" sx={{ justifyContent: "space-between" }}>
              <Button
                color="inherit"
                disabled={page === 1 || folders.isFetching}
                onClick={() => setPage(page - 1)}
              >
                {t("previous")}
              </Button>
              <Button
                color="inherit"
                disabled={!folders.data?.hasMore || folders.isFetching}
                onClick={() => setPage(page + 1)}
              >
                {t("next")}
              </Button>
            </Stack>
          )}
        </Stack>
      </DialogContent>
      <DialogActions>
        <Button color="inherit" onClick={() => setOpen(false)}>
          {t("cancel")}
        </Button>
        <Button
          variant="contained"
          disabled={!current || folders.isFetching}
          onClick={() => {
            if (current) {
              onSelect(current);
              setOpen(false);
            }
          }}
        >
          {t("useFolder")}
        </Button>
      </DialogActions>
    </Dialog>
  );
};
