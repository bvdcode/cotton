import { defineConfig, loadEnv } from "vite";
import react from "@vitejs/plugin-react";
import { VitePWA } from "vite-plugin-pwa";

const NODE_MODULES_MARKER = "/node_modules/";

function getNodeModulePath(id: string): string | undefined {
  const normalizedId = id.replaceAll("\\", "/");
  const markerIndex = normalizedId.lastIndexOf(NODE_MODULES_MARKER);

  return markerIndex === -1
    ? undefined
    : normalizedId.slice(markerIndex + NODE_MODULES_MARKER.length);
}

// https://vite.dev/config/
export default defineConfig(({ mode }) => {
  const env = loadEnv(mode, process.cwd(), "");
  const apiTarget = env.VITE_API_TARGET || "http://localhost:5182";

  return {
    server: {
      proxy: {
        "/api": {
          target: apiTarget,
          changeOrigin: true,
          secure: true,
          ws: true,
        },
        "^/s/[^/]+": {
          target: apiTarget,
          changeOrigin: true,
          secure: true,
          ws: true,
        },
        "/api/v1/hub": {
          target: apiTarget,
          changeOrigin: true,
          secure: true,
          ws: true,
        },
      },
    },
    build: {
      rollupOptions: {
        output: {
          manualChunks(id) {
            const modulePath = getNodeModulePath(id);
            if (!modulePath) {
              return undefined;
            }

            if (
              modulePath.startsWith("three/") ||
              modulePath.startsWith("@react-three/")
            ) {
              return "vendor-three";
            }

            if (
              modulePath.startsWith("monaco-editor/") ||
              modulePath.startsWith("@monaco-editor/")
            ) {
              return "vendor-monaco";
            }

            if (modulePath.startsWith("pdfjs-dist/")) {
              return "vendor-pdfjs";
            }

            if (modulePath.startsWith("@mui/x-data-grid")) {
              return "vendor-mui-datagrid";
            }

            if (modulePath.startsWith("@mui/icons-material/")) {
              return "vendor-mui-icons";
            }

            if (
              modulePath.startsWith("@mui/material/") ||
              modulePath.startsWith("@mui/system/") ||
              modulePath.startsWith("@mui/styled-engine/") ||
              modulePath.startsWith("@emotion/")
            ) {
              return "vendor-mui";
            }

            if (modulePath.startsWith("@microsoft/signalr/")) {
              return "vendor-signalr";
            }

            return undefined;
          },
        },
      },
    },
    plugins: [
      react(),
      VitePWA({
        registerType: "prompt",
        injectRegister: false,
        includeAssets: ["/favicon.ico", "/assets/icons/icon.svg"],
        workbox: {
          sourcemap: true,
          cleanupOutdatedCaches: true,
          // Important: share links (/s/:token) are served by the backend and may
          // respond with a file (e.g. ?view=download). In some browsers (Firefox)
          // a download click is a navigation request, and Workbox's default
          // navigate fallback can incorrectly serve index.html instead of the file.
          // Denylist /s/* from navigate fallback so it always hits the network.
          navigateFallbackDenylist: [
            /^\/s\//,
            /^\/api\//,
            /^\/files\//,
            /^\/chunks\//,
            /^\/preview\//,
          ],
          // Allow larger bundles to be precached in CI (default is 2 MiB)
          maximumFileSizeToCacheInBytes: 6 * 1024 * 1024,
        },
        manifest: {
          id: "/",
          name: "Cotton Cloud",
          short_name: "Cotton",
          description: "Fast and reliable cloud service for your needs.",
          categories: ["cloud", "storage", "productivity"],
          lang: "en-US",
          scope: "/",
          start_url: "/",
          display: "standalone",
          background_color: "#2c2d2e",
          theme_color: "#c6ff00",
          screenshots: [
            {
              src: "/assets/images/screenshot1.jpg",
              sizes: "720x1280",
              type: "image/jpeg",
              form_factor: "narrow",
            },
            {
              src: "/assets/images/screenshot3.jpg",
              sizes: "720x1280",
              type: "image/jpeg",
              form_factor: "narrow",
            },
            {
              src: "/assets/images/screenshot5.jpg",
              sizes: "720x1280",
              type: "image/jpeg",
              form_factor: "narrow",
            },
            {
              src: "/assets/images/screenshot2.jpg",
              sizes: "1920x1080",
              type: "image/jpeg",
              form_factor: "wide",
            },
          ],
          icons: [
            {
              src: "/assets/icons/icon.svg",
              sizes: "any",
              type: "image/svg+xml",
              purpose: "any",
            },
            {
              src: "/assets/icons/icon-192.png",
              sizes: "192x192",
              type: "image/png",
              purpose: "any",
            },
            {
              src: "/assets/icons/icon-512.png",
              sizes: "512x512",
              type: "image/png",
              purpose: "any",
            },
            {
              src: "/assets/icons/icon-maskable-192.png",
              sizes: "192x192",
              type: "image/png",
              purpose: "maskable",
            },
            {
              src: "/assets/icons/icon-maskable-512.png",
              sizes: "512x512",
              type: "image/png",
              purpose: "maskable",
            },
            {
              src: "/assets/icons/icon-monochrome.svg",
              sizes: "512x512",
              type: "image/svg+xml",
              purpose: "monochrome",
            },
          ],
        },
      }),
    ],
  };
});
