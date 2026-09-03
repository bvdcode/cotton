import { defineConfig } from "vite";
import react from "@vitejs/plugin-react";
import { VitePWA } from "vite-plugin-pwa";
import { fileURLToPath, URL } from "node:url";

// https://vite.dev/config/
export default defineConfig(() => {
  const apiTarget = "https://app.cottoncloud.dev";

  return {
    resolve: {
      alias: {
        "@app": fileURLToPath(new URL("./src/app", import.meta.url)),
        "@features": fileURLToPath(new URL("./src/features", import.meta.url)),
        "@pages": fileURLToPath(new URL("./src/pages", import.meta.url)),
        "@shared": fileURLToPath(new URL("./src/shared", import.meta.url)),
      },
    },
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
      chunkSizeWarningLimit: 10 * 1024,
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
          maximumFileSizeToCacheInBytes: 10 * 1024 * 1024,
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
