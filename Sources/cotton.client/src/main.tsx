import "./index.css";
import App from "./App.tsx";
import { StrictMode } from "react";
import "./shared/i18n/locales/config";
import { createRoot } from "react-dom/client";

createRoot(document.getElementById("root")!).render(
  <StrictMode>
    <App />
  </StrictMode>,
);
