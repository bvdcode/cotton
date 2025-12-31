import { useEffect, useRef } from "react";
import { uploadManager } from "../../../shared/upload/UploadManager";

export const UploadFilePicker = () => {
  const inputRef = useRef<HTMLInputElement | null>(null);

  useEffect(() => {
    uploadManager.setFilePickerOpen(async ({ multiple, accept }) => {
      const showOpenFilePicker = (window as unknown as { showOpenFilePicker?: (options?: unknown) => Promise<Array<{ getFile: () => Promise<File> }>> }).showOpenFilePicker;

      if (typeof showOpenFilePicker === "function") {
        try {
          const handles = await showOpenFilePicker({
            multiple,
            excludeAcceptAllOption: false,
          });
          const files = await Promise.all(handles.map((h) => h.getFile()));
          if (files.length > 0) {
            uploadManager.handleFilePickerSelection(files);
          }
          return;
        } catch {
          // User cancelled or browser denied: fall back to input.
        }
      }

      const input = inputRef.current;
      if (!input) return;

      input.multiple = multiple;
      input.accept = accept ?? "*/*";
      input.click();
    });

    return () => {
      uploadManager.setFilePickerOpen(null);
    };
  }, []);

  return (
    <input
      ref={inputRef}
      type="file"
      style={{ display: "none" }}
      onChange={(e) => {
        const files = e.currentTarget.files;
        if (files && files.length > 0) {
          uploadManager.handleFilePickerSelection(files);
        }
        e.currentTarget.value = "";
      }}
    />
  );
};
