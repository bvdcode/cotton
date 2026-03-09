import { useEffect, useRef } from "react";
import { uploadManager } from "../../../shared/upload/UploadManager";

type FilePickerHandle = {
  getFile: () => Promise<File>;
};

type FilePickerOptions = {
  multiple?: boolean;
  excludeAcceptAllOption?: boolean;
};

type FilePickerWindow = Window & {
  showOpenFilePicker?: (
    options?: FilePickerOptions,
  ) => Promise<FilePickerHandle[]>;
};

export const UploadFilePicker = () => {
  const inputRef = useRef<HTMLInputElement | null>(null);

  useEffect(() => {
    uploadManager.setFilePickerOpen(async ({ multiple, accept }) => {
      const showOpenFilePicker = (window as FilePickerWindow).showOpenFilePicker;

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
      hidden
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
