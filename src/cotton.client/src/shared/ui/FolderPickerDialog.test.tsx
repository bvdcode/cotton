import { act, cleanup, render } from "@testing-library/react";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { ThemeProvider } from "@mui/material";
import { Profiler } from "react";
import { afterEach, expect, it, vi } from "vitest";
import { layoutsApi } from "../api/layoutsApi";
import { httpClient } from "../api/httpClient";
import { generateMasterKey } from "../crypto/keys";
import { useVault } from "../crypto/vault";
import { darkTheme } from "../theme";
import { createFolder } from "../../test/fileFixtures";
import { FolderPickerDialog } from "./FolderPickerDialog";

vi.mock("react-i18next", () => ({
  useTranslation: () => ({ t: (key: string) => key }),
}));
afterEach(() => {
  cleanup();
  useVault.getState().lock();
  vi.restoreAllMocks();
});
it("settles while the folder request is pending with the vault unlocked", async () => {
  useVault
    .getState()
    .unlock(await generateMasterKey(), { persistToSession: false });
  vi.spyOn(layoutsApi, "resolve").mockResolvedValue(createFolder());
  vi.spyOn(httpClient, "get").mockReturnValue(new Promise(() => {}));
  const client = new QueryClient({
    defaultOptions: { queries: { retry: false } },
  });
  let renders = 0;
  const view = render(
    <ThemeProvider theme={darkTheme}>
      <QueryClientProvider client={client}>
        <Profiler
          id="folder-picker"
          onRender={() => {
            renders++;
            if (renders > 20) {
              throw new Error("Folder picker does not settle during loading");
            }
          }}
        >
          <FolderPickerDialog
            initialNodeId="folder-1"
            onSelect={vi.fn()}
            onClose={vi.fn()}
          />
        </Profiler>
      </QueryClientProvider>
    </ThemeProvider>,
  );
  await act(async () => {
    await Promise.resolve();
  });
  const settledRenders = renders;
  await act(async () => {
    await Promise.resolve();
  });
  expect(renders).toBe(settledRenders);
  expect(renders).toBeLessThan(10);
  view.unmount();
  client.clear();
});
