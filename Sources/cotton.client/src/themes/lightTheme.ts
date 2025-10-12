import { createTheme } from "@mui/material/styles";

export const lightTheme = createTheme({
  typography: {
    fontFamily:
      '"Roboto", system-ui, -apple-system, "Segoe UI", Roboto, "Helvetica Neue", Arial, "Apple Color Emoji", "Segoe UI Emoji"',
  },
  palette: {
    mode: "light",
    primary: { main: "#fe6c00" },
    secondary: { main: "#06B6D4" },
    background: {
      default: "#f7f7f9",
      paper: "#ffffff",
    },
    divider: "rgba(0,0,0,.08)",
    text: {
      primary: "#1a1a1a",
      secondary: "rgba(0,0,0,.6)",
    },
  },
  shape: { borderRadius: 12 },
  components: {
    MuiCssBaseline: {
      styleOverrides: `
        /* Neutralize Chrome's autofill background */
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
          -webkit-text-fill-color: #1a1a1a !important;
          caret-color: #1a1a1a;
          transition: background-color 50000s ease-in-out 0s !important;
          border-radius: inherit;
        }
        /* Native date input calendar icon coloring (WebKit/Blink) */
        input[type="date"]::-webkit-calendar-picker-indicator {
          filter: invert(25%) sepia(16%) saturate(500%) hue-rotate(220deg) brightness(95%);
          opacity: 0.75;
          transition: opacity .2s;
        }
        input[type="date"]:hover::-webkit-calendar-picker-indicator {
          opacity: 1;
        }
        .theme-dark input[type="date"]::-webkit-calendar-picker-indicator {
          filter: invert(85%) sepia(10%) saturate(300%) hue-rotate(180deg) brightness(105%);
          opacity: 0.8;
        }
        html {
          scrollbar-width: thin;
          scrollbar-color: rgba(55, 113, 190, 0.45) rgba(52, 58, 64, 0.12);
        }
        *, *::before, *::after {
          scrollbar-width: thin;
        }
        *::-webkit-scrollbar {
          width: 10px;
          height: 10px;
        }
        *::-webkit-scrollbar-track {
          background: rgba(52, 58, 64, 0.12);
          border-radius: 9999px;
        }
        *::-webkit-scrollbar-thumb {
          background-color: rgba(55, 113, 190, 0.65);
          border-radius: 9999px;
          border: 2px solid rgba(248, 249, 250, 0.9);
        }
        *::-webkit-scrollbar-thumb:hover {
          background-color: rgba(55, 113, 190, 0.8);
        }
      `,
    },
  },
});

export default lightTheme;
