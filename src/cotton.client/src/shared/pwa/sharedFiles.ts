import {
  SHARE_TARGET_FILE_FIELD,
  SHARE_TARGET_PATH,
} from "./shareTargetConfig";

const SHARED_FILES_CACHE = "cotton-shared-files";
const SHARED_FILES_MAX_AGE_MS = 24 * 60 * 60 * 1000;

const cacheUrl = (id: string): string =>
  new URL(`${SHARE_TARGET_PATH}/${encodeURIComponent(id)}`, location.origin)
    .href;

const isExpired = (response: Response): boolean => {
  const receivedAt = Date.parse(response.headers.get("Date") ?? "");
  return (
    !Number.isFinite(receivedAt) ||
    Date.now() - receivedAt > SHARED_FILES_MAX_AGE_MS
  );
};

export const getSharedFiles = (data: FormData): File[] =>
  data.getAll(SHARE_TARGET_FILE_FIELD).filter((entry) => entry instanceof File);

export const saveSharedFiles = async (files: File[]): Promise<string> => {
  const cache = await caches.open(SHARED_FILES_CACHE);
  for (const request of await cache.keys()) {
    const response = await cache.match(request);
    if (response && isExpired(response)) {
      await cache.delete(request);
    }
  }

  const id = crypto.randomUUID();
  const data = new FormData();
  for (const file of files) {
    data.append(SHARE_TARGET_FILE_FIELD, file);
  }
  await cache.put(
    cacheUrl(id),
    new Response(data, { headers: { Date: new Date().toUTCString() } }),
  );
  return id;
};

export const uploadSharedFiles = async (
  id: string,
  upload: (files: File[]) => Promise<void>,
): Promise<boolean> =>
  navigator.locks.request(`${SHARED_FILES_CACHE}:${id}`, async () => {
    const cache = await caches.open(SHARED_FILES_CACHE);
    const url = cacheUrl(id);
    const response = await cache.match(url);
    if (!response) {
      return false;
    }
    if (isExpired(response)) {
      await cache.delete(url);
      return false;
    }

    const files = getSharedFiles(await response.formData());
    if (files.length === 0) {
      await cache.delete(url);
      return false;
    }

    await upload(files);
    await cache.delete(url);
    return true;
  });
