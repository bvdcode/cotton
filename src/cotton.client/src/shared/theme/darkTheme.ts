import { createTheme } from "@mui/material/styles";

export const darkTheme = createTheme({
  typography: {
    fontFamily:
      '"Roboto", system-ui, -apple-system, "Segoe UI", Roboto, "Helvetica Neue", Arial, "Apple Color Emoji", "Segoe UI Emoji"',
  },
  palette: {
    mode: "dark",
    primary: { main: "#5d32adff" },
    secondary: { main: "#1bcea7ff" },
    background: {
      default: "#1f2022",
      paper: "#151A21",
    },
    divider: "rgba(255,255,255,.06)",
    text: {
      primary: "#ffffff",
      secondary: "#cecece",
    },
  },
  shape: { borderRadius: 12 },
  components: {
    MuiCssBaseline: {
      styleOverrides: `
        input:-webkit-autofill,
        input:-webkit-autofill:hover,
        input:-webkit-autofill:focus,
        input:-webkit-autofill:active,
        textarea:-webkit-autofill,
        textarea:-webkit-autofill:hover,
        textarea:-webkit-autofill:focus,
        select:-webkit-autofill,
        select:-webkit-autofill:hover,
        select:-webkit-autofill:focus,
        .MuiInputBase-input:-webkit-autofill {
          -webkit-box-shadow: 0 0 0px 1000px rgba(255,255,255,0.04) inset !important;
          box-shadow: 0 0 0px 1000px rgba(0, 0, 0, 0) inset !important;
          -webkit-text-fill-color: #fff !important;
          caret-color: #fff;
          transition: background-color 50000s ease-in-out 0s !important;
          border-radius: inherit;
        }
        input[type="date"]::-webkit-calendar-picker-indicator {
          filter: invert(85%) sepia(10%) saturate(300%) hue-rotate(180deg) brightness(105%);
          opacity: 0.75;
          transition: opacity .2s;
        }
        input[type="date"]:hover::-webkit-calendar-picker-indicator {
          opacity: 1;
        }
        .theme-light input[type="date"]::-webkit-calendar-picker-indicator {
          filter: invert(25%) sepia(16%) saturate(500%) hue-rotate(220deg) brightness(95%);
        }
        html { color-scheme: dark; }
        :root { color-scheme: dark; }
      `,
    },
  },
});

export default darkTheme;
