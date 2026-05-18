import "./i18n.ts";
import "./index.css";
import App from "./App.tsx";
import { StrictMode } from "react";
import { createRoot } from "react-dom/client";
import { registerServiceWorker } from "./shared/pwa/registerServiceWorker.ts";
import { installStaleChunkReloadHandler } from "./shared/utils/staleChunkReload.ts";

registerServiceWorker();
installStaleChunkReloadHandler();

createRoot(document.getElementById("root")!).render(
  <StrictMode>
    <App />
  </StrictMode>,
);
