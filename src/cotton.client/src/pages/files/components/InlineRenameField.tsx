import React, { useRef, useState } from "react";
import { CircularProgress, Input } from "@mui/material";
import { useTranslation } from "react-i18next";

interface InlineRenameFieldProps {
  value: string;
  onChange: (value: string) => void;
  onConfirm: () => void | Promise<void>;
  onCancel: () => void;
  placeholder?: string;
}

const stopEventPropagation = (event: React.SyntheticEvent): void => {
  event.stopPropagation();
};

const stopDragStart = (event: React.DragEvent): void => {
  event.preventDefault();
  event.stopPropagation();
};

export const InlineRenameField: React.FC<InlineRenameFieldProps> = ({
  value,
  onChange,
  onConfirm,
  onCancel,
  placeholder,
}) => {
  const { t } = useTranslation();
  const submitting = useRef(false);
  const [pending, setPending] = useState(false);
  const confirm = async () => {
    if (submitting.current) {
      return;
    }
    submitting.current = true;
    setPending(true);
    try {
      await onConfirm();
    } finally {
      submitting.current = false;
      setPending(false);
    }
  };
  const handleKeyDown = (event: React.KeyboardEvent<HTMLInputElement>) => {
    event.stopPropagation();

    if (event.key === "Enter") {
      event.preventDefault();
      void confirm();
      return;
    }

    if (event.key === "Escape") {
      event.preventDefault();
      if (!submitting.current) {
        submitting.current = true;
        onCancel();
      }
    }
  };

  return (
    <Input
      autoFocus
      fullWidth
      readOnly={pending}
      endAdornment={
        pending ? (
          <CircularProgress
            color="inherit"
            size={16}
            aria-label={t("files:loading.title")}
          />
        ) : undefined
      }
      value={value}
      placeholder={placeholder}
      onChange={(event) => onChange(event.target.value)}
      onFocus={(event) => event.target.select()}
      onKeyDown={handleKeyDown}
      onBlur={() => {
        void confirm();
      }}
      onClick={stopEventPropagation}
      onMouseDown={stopEventPropagation}
      onDoubleClick={stopEventPropagation}
      onDragStart={stopDragStart}
      sx={{
        fontSize: "inherit",
        fontWeight: "inherit",
        lineHeight: "inherit",
        "& input": { p: 0, height: "auto" },
      }}
    />
  );
};
