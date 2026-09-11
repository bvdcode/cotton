import type { ReactNode } from "react";
import type { JsonValue } from "../../shared/types/json";

export interface SetupSingleOption<TValue extends JsonValue = JsonValue> {
  key: string;
  label: () => string;
  description?: () => string;
  value: TValue;
  icon?: ReactNode;
  disabledIfAny?: string[];
  requires?: string;
}

export interface SetupMultiOption {
  key: string;
  label: () => string;
  description?: () => string;
  icon?: ReactNode;
  disabledIfAny?: string[];
  requires?: string;
}

export interface SetupTextFieldOption {
  key: string;
  label: () => string;
  placeholder?: () => string;
  type?: "text" | "password" | "url" | "boolean";
}

interface SetupStepBase {
  key: string;
  title: () => string;
  subtitle: () => string;
  linkUrl?: string;
  linkAria?: () => string;
  requires?: string;
}

export interface SetupSingleStepDefinition extends SetupStepBase {
  type: "single";
  extraHeader?: () => ReactNode;
  options: SetupSingleOption[];
  getOptions?: () => SetupSingleOption[];
  getDefaultValue?: () => JsonValue;
  renderAs?: "cards" | "dropdown" | "autocomplete";
}

export interface SetupMultiStepDefinition extends SetupStepBase {
  type: "multi";
  options: SetupMultiOption[];
}

export interface SetupFormStepDefinition extends SetupStepBase {
  type: "form";
  fields: SetupTextFieldOption[];
}

export type SetupStepDefinition =
  | SetupSingleStepDefinition
  | SetupMultiStepDefinition
  | SetupFormStepDefinition;

export interface SetupRenderedOption<TValue extends JsonValue = JsonValue> {
  key: string;
  label: string;
  description?: string;
  value: TValue;
  icon?: ReactNode;
  disabled?: boolean;
  disabledTooltip?: string;
}

export interface SetupRenderedMultiOption {
  key: string;
  label: string;
  description?: string;
  icon?: ReactNode;
  disabled?: boolean;
  disabledTooltip?: string;
}

export interface SetupRenderedFormField {
  key: string;
  label: string;
  placeholder?: string;
  type?: "text" | "password" | "url" | "boolean";
}

export type SetupFormValues = Record<string, string | boolean>;
