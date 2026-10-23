import { useEffect, useState } from "react";
import { Alert, Box, LinearProgress } from "@mui/material";
import { useTranslation } from "react-i18next";
import { reportClientError } from "../../../utils/clientDiagnostics";
import { renderOfficeDocument } from "./renderOfficeDocument";

const frameDocument = `<!doctype html><html><head><meta charset="utf-8">
<meta http-equiv="Content-Security-Policy" content="default-src 'none'; img-src data: blob:; font-src data: blob:; style-src 'unsafe-inline'; media-src data: blob:; base-uri 'none'; form-action 'none'">
<style>html,body{margin:0;min-height:100%}body{overflow-x:auto}.docx-wrapper{padding:12px!important}.docx-wrapper>section.docx{margin-bottom:12px!important}#document{min-width:0}</style>
</head><body><div id="document"></div></body></html>`;

export default function OfficeDocumentPreview({
  bytes,
  format,
  title,
}: {
  bytes: ArrayBuffer;
  format: "docx" | "pptx";
  title: string;
}) {
  const { t } = useTranslation("files");
  const [frame, setFrame] = useState<HTMLIFrameElement | null>(null);
  const [state, setState] = useState<"loading" | "ready" | "failed">("loading");
  useEffect(() => {
    const document = frame?.contentDocument;
    const root = document?.getElementById("document");
    if (!document || !root) {
      return;
    }
    const container = document.createElement("div");
    root.append(container);
    const controller = new AbortController();
    let dispose: (() => void) | undefined;
    void renderOfficeDocument(bytes, format, container, controller.signal)
      .then((cleanup) => {
        if (controller.signal.aborted) {
          cleanup();
        } else {
          dispose = cleanup;
          setState("ready");
        }
      })
      .catch((error) => {
        if (!controller.signal.aborted) {
          reportClientError("Failed to render Office document", error);
          setState("failed");
        }
      });
    return () => {
      controller.abort();
      dispose?.();
      container.remove();
    };
  }, [bytes, format, frame]);
  return (
    <Box
      sx={{ display: "flex", flexDirection: "column", flex: 1, minHeight: 0 }}
    >
      {state === "loading" && <LinearProgress />}
      {state === "failed" && (
        <Alert severity="error">
          {t("preview.errors.loadFailed", { error: "" })}
        </Alert>
      )}
      <Box
        component="iframe"
        title={title}
        srcDoc={frameDocument}
        sandbox="allow-same-origin"
        onLoad={(event) => setFrame(event.currentTarget)}
        sx={{
          flex: 1,
          width: "100%",
          minHeight: 0,
          border: 0,
          display: state === "failed" ? "none" : "block",
        }}
      />
    </Box>
  );
}
