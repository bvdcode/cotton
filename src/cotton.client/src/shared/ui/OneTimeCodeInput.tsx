import { Box, TextField } from "@mui/material";
import { useEffect, useMemo, useRef } from "react";

export interface OneTimeCodeInputProps {
  value: string;
  onChange: (next: string) => void;
  length?: number;
  disabled?: boolean;
  autoFocus?: boolean;
  inputAriaLabel?: string;
}

const normalize = (raw: string, length: number) => {
  const digits = raw.replace(/\D/g, "");
  return digits.slice(0, length);
};

export const OneTimeCodeInput = ({
  value,
  onChange,
  length = 6,
  disabled = false,
  autoFocus = false,
  inputAriaLabel,
}: OneTimeCodeInputProps) => {
  const refs = useRef<Array<HTMLInputElement | null>>([]);

  const digits = useMemo(() => {
    const clean = normalize(value, length);
    const padded = clean.padEnd(length, " ");
    return padded.split("").map((c) => (c === " " ? "" : c));
  }, [value, length]);

  useEffect(() => {
    if (!autoFocus) return;
    const first = refs.current[0];
    first?.focus();
  }, [autoFocus]);

  const setAt = (index: number, digit: string) => {
    const clean = normalize(value, length);
    const arr = clean.split("");
    while (arr.length < length) arr.push("");
    arr[index] = digit;
    onChange(normalize(arr.join(""), length));
  };

  const moveFocus = (nextIndex: number) => {
    const el = refs.current[nextIndex];
    el?.focus();
    el?.select?.();
  };

  return (
    <Box sx={{ display: "flex", gap: { xs: 1, sm: 1.25 } }}>
      {Array.from({ length }).map((_: unknown, i: number) => (
        <TextField
          key={i}
          inputRef={(el) => {
            refs.current[i] = el;
          }}
          value={digits[i] ?? ""}
          disabled={disabled}
          variant="outlined"
          size="small"
          inputProps={{
            inputMode: "numeric",
            pattern: "[0-9]*",
            maxLength: 1,
            style: { textAlign: "center", fontWeight: 700 },
            "aria-label": inputAriaLabel ? `${inputAriaLabel} ${i + 1}` : undefined,
          }}
          sx={{ width: 56 }}
          onChange={(e) => {
            const next = normalize(e.target.value, 1);
            if (next.length === 0) {
              setAt(i, "");
              return;
            }
            setAt(i, next);
            if (i < length - 1) {
              moveFocus(i + 1);
            }
          }}
          onKeyDown={(e) => {
            if (e.key === "Backspace") {
              if ((digits[i] ?? "").length > 0) {
                setAt(i, "");
                return;
              }
              if (i > 0) {
                moveFocus(i - 1);
              }
            }
            if (e.key === "ArrowLeft" && i > 0) {
              moveFocus(i - 1);
            }
            if (e.key === "ArrowRight" && i < length - 1) {
              moveFocus(i + 1);
            }
          }}
          onPaste={(e) => {
            const pasted = normalize(e.clipboardData.getData("text"), length);
            if (!pasted) return;
            e.preventDefault();
            onChange(pasted);
            const nextIndex = Math.min(pasted.length, length - 1);
            moveFocus(nextIndex);
          }}
        />
      ))}
    </Box>
  );
};
