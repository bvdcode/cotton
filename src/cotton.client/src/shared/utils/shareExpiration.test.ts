import { beforeEach, afterEach, expect, it, vi } from "vitest";
import i18n from "../../i18n";
import { filesApi } from "../api/filesApi";
import { layoutsApi } from "../api/layoutsApi";
import { httpClient } from "../api/httpClient";
import {
  userPreferencesApi,
  type UserPreferences,
} from "../api/userPreferencesApi";
import {
  useUserPreferencesStore,
  selectShareLinkExpireAfterMinutes,
} from "../store/userPreferencesStore";
import { toast } from "../ui/notifications";
import { shareFile } from "./shareFile";
import { shareFolder } from "./shareFolder";
import { shareLinkAction } from "./shareLinkAction";

vi.mock("./shareLinkAction", () => ({ shareLinkAction: vi.fn() }));

beforeEach(async () => {
  await i18n.changeLanguage("en");
  useUserPreferencesStore.getState().reset();
});
afterEach(() => vi.restoreAllMocks());

it("distinguishes an unset default from explicitly unlimited expiration", () => {
  expect(
    selectShareLinkExpireAfterMinutes(useUserPreferencesStore.getState()),
  ).toBe(43200);
  useUserPreferencesStore
    .getState()
    .hydrateFromRemote({ shareLinkExpireAfterMinutes: "null" });
  expect(
    selectShareLinkExpireAfterMinutes(useUserPreferencesStore.getState()),
  ).toBeNull();
});

it("serializes unlimited file and folder links as explicit empty nullable query values", async () => {
  const get = vi
    .spyOn(httpClient, "get")
    .mockResolvedValue({ data: "/s/token" });
  await filesApi.getShareLink("file", null);
  await layoutsApi.getNodeShareLink("folder", null);
  for (const [url, options] of get.mock.calls) {
    expect(httpClient.getUri({ ...options, url })).toContain(
      "expireAfterMinutes=",
    );
    expect(options?.params).toMatchObject({ expireAfterMinutes: "" });
  }
  expect(get).toHaveBeenCalledTimes(2);
});

it.each([43200, null])(
  "shows the actual expiration once across file and folder shares (%s)",
  async (minutes) => {
    let saved: UserPreferences = {
      shareLinkExpireAfterMinutes: String(minutes),
    };
    useUserPreferencesStore.getState().hydrateFromRemote(saved);
    vi.spyOn(userPreferencesApi, "update").mockImplementation(async (patch) => {
      saved = { ...saved, ...patch };
      return saved;
    });
    vi.spyOn(filesApi, "getShareLink").mockResolvedValue("/s/file-token");
    vi.spyOn(layoutsApi, "getNodeShareLink").mockResolvedValue(
      "/s/folder-token",
    );
    vi.spyOn(toast, "success").mockReturnValue("success");
    const notice = vi.spyOn(toast, "info").mockReturnValue("notice");
    vi.mocked(shareLinkAction).mockResolvedValue({ kind: "aborted" });
    await shareFile("file", "test.txt", i18n.t);
    expect(notice).not.toHaveBeenCalled();
    vi.mocked(shareLinkAction).mockResolvedValue({ kind: "copied" });
    await shareFile("file", "test.txt", i18n.t);
    await shareFolder("folder", "Folder", i18n.t);
    expect(notice).toHaveBeenCalledTimes(1);
    expect(notice.mock.calls[0][0]).toContain(
      minutes === null ? "Never" : "Month",
    );
    expect(
      useUserPreferencesStore.getState().preferences
        .shareLinkExpirationNoticeShown,
    ).toBe("true");
  },
);
