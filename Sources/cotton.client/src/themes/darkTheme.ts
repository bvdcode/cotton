import { createTheme } from "@mui/material/styles";

export const darkTheme = createTheme({
  typography: {
    fontFamily:
      '"Roboto", system-ui, -apple-system, "Segoe UI", Roboto, "Helvetica Neue", Arial, "Apple Color Emoji", "Segoe UI Emoji"',
  },
  palette: {
    mode: "dark",
    primary: { main: "#3771be" },
    secondary: { main: "#ce1b30" },
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
        /* Neutralize Chrome's blue/yellow autofill background */
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
          box-shadow: 0 0 0px 1000px rgba(255,255,255,0.04) inset !important;
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
        html {
          scrollbar-width: thin;
          scrollbar-color: rgba(80, 139, 210, 0.55) rgba(255, 255, 255, 0.08);
        }
        *, *::before, *::after {
          scrollbar-width: thin;
        }
        *::-webkit-scrollbar {
          width: 10px;
          height: 10px;
        }
        *::-webkit-scrollbar-track {
          background: rgba(255, 255, 255, 0.08);
          border-radius: 9999px;
        }
        *::-webkit-scrollbar-thumb {
          background-color: rgba(80, 139, 210, 0.65);
          border-radius: 9999px;
          border: 2px solid rgba(33, 37, 41, 0.95);
        }
        *::-webkit-scrollbar-thumb:hover {
          background-color: rgba(80, 139, 210, 0.8);
        }
      `,
    },
  },
});

export default darkTheme;
