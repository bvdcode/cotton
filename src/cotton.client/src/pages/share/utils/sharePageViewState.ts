export type ShareTargetKind = "resolving" | "file" | "folder";

export interface SharedFolderInfo {
  nodeId: string;
  name: string;
}

export type SharePageViewState =
  | { kind: "loading" }
  | { kind: "folder"; token: string; folder: SharedFolderInfo }
  | { kind: "file"; token: string; inlineUrl: string }
  | { kind: "file-error"; message: string };

interface ResolveSharePageViewStateArgs {
  token: string | null;
  targetKind: ShareTargetKind;
  loading: boolean;
  resolvedError: string | null;
  resolvedInlineUrl: string | null;
  sharedFolderInfo: SharedFolderInfo | null;
}

export const resolveSharePageViewState = (
  args: ResolveSharePageViewStateArgs,
): SharePageViewState => {
  const {
    token,
    targetKind,
    loading,
    resolvedError,
    resolvedInlineUrl,
    sharedFolderInfo,
  } = args;

  if (token !== null && targetKind === "resolving") {
    return { kind: "loading" };
  }

  if (targetKind === "folder" && token !== null && sharedFolderInfo !== null) {
    return {
      kind: "folder",
      token,
      folder: sharedFolderInfo,
    };
  }

  if (targetKind === "file" && loading) {
    return { kind: "loading" };
  }

  if (targetKind === "file" && resolvedError) {
    return {
      kind: "file-error",
      message: resolvedError,
    };
  }

  if (targetKind === "file" && token !== null && resolvedInlineUrl !== null) {
    return {
      kind: "file",
      token,
      inlineUrl: resolvedInlineUrl,
    };
  }

  return {
    kind: "file-error",
    message: resolvedError ?? "",
  };
};
