/// <reference lib="webworker" />

import {
  cleanupOutdatedCaches,
  createHandlerBoundToURL,
  precacheAndRoute,
} from "workbox-precaching";
import { NavigationRoute, registerRoute } from "workbox-routing";
import { receiveSharedFiles } from "./shared/pwa/receiveSharedFiles";
import { SHARE_TARGET_PATH } from "./shared/pwa/shareTargetConfig";

declare const self: ServiceWorkerGlobalScope & {
  __WB_MANIFEST: Parameters<typeof precacheAndRoute>[0];
};

self.addEventListener("message", (event) => {
  if (event.data?.type === "SKIP_WAITING") {
    void self.skipWaiting();
  }
});

cleanupOutdatedCaches();
precacheAndRoute(self.__WB_MANIFEST);

registerRoute(
  new NavigationRoute(createHandlerBoundToURL("index.html"), {
    denylist: [
      /^\/s\//,
      /^\/api\//,
      /^\/files\//,
      /^\/chunks\//,
      /^\/preview\//,
    ],
  }),
);

registerRoute(
  ({ url }) =>
    url.origin === self.location.origin && url.pathname === SHARE_TARGET_PATH,
  ({ request }) => receiveSharedFiles(request),
  "POST",
);
