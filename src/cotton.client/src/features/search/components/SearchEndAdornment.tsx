import { CircularProgress, IconButton, Typography } from "@mui/material";
import { Close } from "@mui/icons-material";

interface SearchEndAdornmentProps {
  isMobile: boolean;
  onClose: () => void;
  resultCount: number;
  resultsCountText: string;
  waitingForResults: boolean;
  closeText: string;
}

export const SearchEndAdornment = ({
  isMobile,
  onClose,
  resultCount,
  resultsCountText,
  waitingForResults,
  closeText,
}: SearchEndAdornmentProps) => {
  if (isMobile) {
    return (
      <IconButton
        edge="end"
        aria-label={closeText}
        title={closeText}
        onClick={onClose}
      >
        <Close />
      </IconButton>
    );
  }

  if (waitingForResults) {
    return <CircularProgress size={24} />;
  }

  if (resultCount === 0) {
    return null;
  }

  return (
    <Typography
      variant="caption"
      color="text.secondary"
      noWrap
      sx={{ px: 0.5 }}
    >
      {resultsCountText}
    </Typography>
  );
};
