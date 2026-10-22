import {
  Box,
  Collapse,
  Divider,
  List,
  ListItemButton,
  Typography,
} from "@mui/material";
import type { AudioPlaylistItem } from "../../shared/types/audio";
import {
  getAudioDisplaySubtitle,
  getAudioDisplayTitle,
} from "../../shared/utils/mediaMetadata";

interface AudioPlayerQueueProps {
  open: boolean;
  playlist: ReadonlyArray<AudioPlaylistItem>;
  currentFileId: string;
  onSelect: (item: AudioPlaylistItem) => void;
}

export const AudioPlayerQueue = ({
  open,
  playlist,
  currentFileId,
  onSelect,
}: AudioPlayerQueueProps) => (
  <Collapse in={open} timeout="auto" unmountOnExit>
    <Divider sx={{ mt: 1 }} />
    <Box
      sx={{
        maxHeight: { xs: 220, sm: 300 },
        overflow: "auto",
      }}
    >
      <List dense disablePadding>
        {playlist.map((item) => {
          const title = getAudioDisplayTitle(item);
          const subtitle = getAudioDisplaySubtitle(item);

          return (
            <ListItemButton
              key={item.id}
              selected={item.id === currentFileId}
              onClick={() => {
                onSelect(item);
              }}
              sx={{
                "&.Mui-selected": {
                  bgcolor: "action.hover",
                },
                "&.Mui-selected:hover": {
                  bgcolor: "action.selected",
                },
              }}
            >
              <Box
                sx={{
                  display: "flex",
                  alignItems: "center",
                  gap: 2,
                  width: "100%",
                  minWidth: 0,
                }}
              >
                <Box
                  sx={{
                    display: "flex",
                    flexDirection: "column",
                    flex: 1,
                    minWidth: 0,
                  }}
                >
                  <Typography variant="body2" noWrap>
                    {title}
                  </Typography>
                  {subtitle ? (
                    <Typography
                      variant="caption"
                      noWrap
                      sx={{
                        color: "text.secondary",
                      }}
                    >
                      {subtitle}
                    </Typography>
                  ) : null}
                </Box>

                {item.folderPath ? (
                  <Typography
                    variant="caption"
                    noWrap
                    sx={{
                      color: "text.secondary",
                      maxWidth: "45%",
                    }}
                  >
                    {item.folderPath}
                  </Typography>
                ) : null}
              </Box>
            </ListItemButton>
          );
        })}
      </List>
    </Box>
  </Collapse>
);
