import React from "react";
import { TextField } from "@mui/material";
import { useTranslation } from "react-i18next";

export interface UserPersonalInfoFieldsProps {
  firstName: string;
  lastName: string;
  birthDate: string;
  onFirstNameChange: (value: string) => void;
  onLastNameChange: (value: string) => void;
  onBirthDateChange: (value: string) => void;
  disabled?: boolean;
}

export const UserPersonalInfoFields: React.FC<UserPersonalInfoFieldsProps> = ({
  firstName,
  lastName,
  birthDate,
  onFirstNameChange,
  onLastNameChange,
  onBirthDateChange,
  disabled = false,
}) => {
  const { t } = useTranslation(["admin"]);

  return (
    <>
      <TextField
        label={t("users.create.firstName")}
        value={firstName}
        onChange={(e) => onFirstNameChange(e.target.value)}
        fullWidth
        autoComplete="given-name"
        disabled={disabled}
      />
      <TextField
        label={t("users.create.lastName")}
        value={lastName}
        onChange={(e) => onLastNameChange(e.target.value)}
        fullWidth
        autoComplete="family-name"
        disabled={disabled}
      />
      <TextField
        label={t("users.create.birthDate")}
        type="date"
        value={birthDate}
        onChange={(e) => onBirthDateChange(e.target.value)}
        fullWidth
        slotProps={{ inputLabel: { shrink: true } }}
        disabled={disabled}
      />
    </>
  );
};
