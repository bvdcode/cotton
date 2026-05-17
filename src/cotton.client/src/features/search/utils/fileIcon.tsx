import {
  Article,
  Image as ImageIcon,
  InsertDriveFile,
  TextSnippet,
  VideoFile,
} from "@mui/icons-material";
import {
  isImageFile,
  isPdfFile,
  isTextFile,
  isVideoFile,
} from "@shared/utils/fileTypes";

export const getSmallFileIcon = (fileName: string) => {
  const iconSx = { fontSize: 28 };
  if (isTextFile(fileName)) return <Article color="action" sx={iconSx} />;
  if (isImageFile(fileName)) return <ImageIcon color="action" sx={iconSx} />;
  if (isVideoFile(fileName)) return <VideoFile color="action" sx={iconSx} />;
  if (isPdfFile(fileName)) return <TextSnippet color="action" sx={iconSx} />;
  return <InsertDriveFile color="action" sx={iconSx} />;
};
