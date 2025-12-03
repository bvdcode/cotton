import { IconButton } from "@mui/material";
import { useThemeMode } from "../contexts/ThemeContext";
import { Brightness4, Brightness7 } from "@mui/icons-material";

export const ThemeToggle = () => {
  const { mode, toggleTheme } = useThemeMode();

  return (
    <IconButton onClick={toggleTheme} color="inherit">
      {mode === "dark" ? <Brightness7 /> : <Brightness4 />}
    </IconButton>
  );
};

export default ThemeToggle;
