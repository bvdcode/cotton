import { reportClientError } from "../utils/clientDiagnostics";
import { getSharedFiles, saveSharedFiles } from "./sharedFiles";
import { SHARE_TARGET_ERROR, SHARE_TARGET_QUERY } from "./shareTargetConfig";

export const receiveSharedFiles = async (
  request: Request,
): Promise<Response> => {
  const sourceOrigin = request.headers.get("Origin");
  if (sourceOrigin !== null && sourceOrigin !== new URL(request.url).origin) {
    return new Response(null, { status: 403 });
  }

  const destination = new URL("/files", request.url);
  try {
    const files = getSharedFiles(await request.formData());
    if (files.length === 0) {
      throw new Error("Share target received no files");
    }
    destination.searchParams.set(
      SHARE_TARGET_QUERY,
      await saveSharedFiles(files),
    );
  } catch (error) {
    reportClientError("Failed to receive shared files", error);
    destination.searchParams.set(SHARE_TARGET_QUERY, SHARE_TARGET_ERROR);
  }
  return Response.redirect(destination.href, 303);
};
