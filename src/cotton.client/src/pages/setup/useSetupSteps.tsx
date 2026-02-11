import { useCallback, useMemo, type ReactNode } from "react";
import { useTranslation } from "react-i18next";
import { setupStepDefinitions } from "./setupQuestions.tsx";
import type { JsonValue } from "../../shared/types/json";
import {
  QuestionBlock,
  QuestionBlockMulti,
  QuestionForm,
  QuestionDropdown,
  QuestionAutocomplete,
} from "./components";

type BuiltStep = {
  key: string;
  render: () => ReactNode;
  isValid: () => boolean;
};

export function useSetupSteps(
  answers: Record<string, JsonValue>,
  updateAnswer: (key: string, value: JsonValue) => void,
  updateFormField: (stepKey: string, fieldKey: string, value: string | boolean) => void,
) {
  const { t } = useTranslation();
  
  // Helper function to check if requirement is met and get required option label and question
  const checkRequires = useCallback((requires?: string): { met: boolean; requiredLabel?: string; questionTitle?: string } => {
    if (!requires) return { met: true };
    
    const [reqKey, reqValue] = requires.split(":");
    const currentValue = answers[reqKey];
    
    let met = false;
    // Check if it's an array (multi-select)
    if (Array.isArray(currentValue)) {
      met = currentValue.includes(reqValue);
    } else {
      met = currentValue === reqValue;
    }
    
    // If not met, get the label of the required option and question title
    if (!met) {
      const step = setupStepDefinitions.find((s) => s.key === reqKey);
      if (step && step.type === "single") {
        const optionsList = "getOptions" in step && step.getOptions 
          ? step.getOptions() 
          : step.options;
        const option = optionsList.find((o) => o.key === reqValue);
        if (option) {
          return { 
            met: false, 
            requiredLabel: option.label(),
            questionTitle: step.title()
          };
        }
      }
    }
    
    return { met };
  }, [answers]);
  
  // Helper function to check if option should be disabled based on answers
  const checkDisabled = useCallback((disabledIfAny?: string[]) => {
    if (!disabledIfAny || disabledIfAny.length === 0) {
      return { disabled: false, reasons: [] };
    }

    const reasons: string[] = [];
    for (const condition of disabledIfAny) {
      const [key, value] = condition.split(":");
      const currentValue = answers[key];
      
      if (currentValue === value) {
        // Find the label for this option
        const step = setupStepDefinitions.find((s) => s.key === key);
        if (step && step.type === "single") {
          const optionsList = "getOptions" in step && step.getOptions 
            ? step.getOptions() 
            : step.options;
          const option = optionsList.find((o) => o.key === value);
          if (option) {
            reasons.push(option.label());
          }
        }
      }
    }

    return {
      disabled: reasons.length > 0,
      reasons,
    };
  }, [answers]);

  const buildSteps = useCallback((): BuiltStep[] => {
    const steps: BuiltStep[] = [];

    for (const def of setupStepDefinitions) {
      // Check if step should be shown based on requires
      if (def.requires) {
        const [reqKey, reqValue] = def.requires.split(":");
        const currentValue = answers[reqKey];
        
        // Check if it's an array (multi-select)
        if (Array.isArray(currentValue)) {
          if (!currentValue.includes(reqValue)) {
            continue;
          }
        } else if (currentValue !== reqValue) {
          continue;
        }
      }

      if (def.type === "single") {
        // Use dynamic options if available, otherwise use static
        const optionsList = def.getOptions ? def.getOptions() : def.options;
        const options = optionsList.map((opt) => {
          const requiresCheck = checkRequires(opt.requires);
          const { disabled, reasons } = checkDisabled(opt.disabledIfAny);
          
          const isDisabled = !requiresCheck.met || disabled;
          let disabledTooltip: string | undefined;
          
          if (!requiresCheck.met && requiresCheck.requiredLabel && requiresCheck.questionTitle) {
            disabledTooltip = `${t("setup:questions.requiresTooltip")} "${requiresCheck.requiredLabel}" ${t("setup:questions.inQuestion")} "${requiresCheck.questionTitle}"`;
          } else if (disabled && reasons.length > 0) {
            disabledTooltip = `${t("setup:questions.telemetry.disabledTooltip")} ${reasons.join(", ")}`;
          }
          
          return {
            key: opt.key,
            label: opt.label(),
            description: opt.description?.(),
            value: opt.value,
            icon: opt.icon,
            disabled: isDisabled,
            disabledTooltip,
          };
        });

        steps.push({
          key: def.key,
          render: () => {
            // Get selected key
            let selectedKey: string | null = null;

            const rawValue = answers[def.key];
            if (typeof rawValue === "string") {
              selectedKey = rawValue;
            } else if (def.getDefaultValue && answers[def.key] === undefined) {
              // Set default value on first render
              const defaultValue = def.getDefaultValue();
              const defaultOption = options.find(opt => opt.value === defaultValue);
              if (defaultOption) {
                selectedKey = defaultOption.key;
                updateAnswer(def.key, selectedKey);
              }
            }

            // Render as dropdown, autocomplete or cards based on renderAs field
            if (def.renderAs === "dropdown") {
              return (
                <QuestionDropdown
                  title={def.title()}
                  subtitle={def.subtitle()}
                  linkUrl={def.linkUrl}
                  linkAriaLabel={def.linkAria?.()}
                  options={options}
                  selectedKey={selectedKey}
                  onSelect={(key) => updateAnswer(def.key, key)}
                />
              );
            }

            if (def.renderAs === "autocomplete") {
              return (
                <QuestionAutocomplete
                  title={def.title()}
                  subtitle={def.subtitle()}
                  linkUrl={def.linkUrl}
                  linkAriaLabel={def.linkAria?.()}
                  options={options}
                  selectedKey={selectedKey}
                  onSelect={(key) => updateAnswer(def.key, key)}
                />
              );
            }

            return (
              <QuestionBlock
                title={def.title()}
                subtitle={def.subtitle()}
                linkUrl={def.linkUrl}
                linkAriaLabel={def.linkAria?.()}
                options={options}
                selectedKey={selectedKey}
                onSelect={(key) => updateAnswer(def.key, key)}
              />
            );
          },
          isValid: (): boolean =>
            typeof answers[def.key] === "string" && answers[def.key] !== "",
        });
      } else if (def.type === "multi") {
        const options = def.options.map((opt) => {
          const requiresCheck = checkRequires(opt.requires);
          const { disabled, reasons } = checkDisabled(opt.disabledIfAny);
          
          const isDisabled = !requiresCheck.met || disabled;
          let disabledTooltip: string | undefined;
          
          if (!requiresCheck.met && requiresCheck.requiredLabel && requiresCheck.questionTitle) {
            disabledTooltip = `${t("setup:questions.requiresTooltip")} "${requiresCheck.requiredLabel}" ${t("setup:questions.inQuestion")} "${requiresCheck.questionTitle}"`;
          } else if (disabled && reasons.length > 0) {
            disabledTooltip = `${t("setup:questions.telemetry.disabledTooltip")} ${reasons.join(", ")}`;
          }
          
          return {
            key: opt.key,
            label: opt.label(),
            description: opt.description?.(),
            icon: opt.icon,
            disabled: isDisabled,
            disabledTooltip,
          };
        });

        steps.push({
          key: def.key,
          render: () => {
            const value = answers[def.key];
            const selectedKeys = Array.isArray(value)
              ? value.filter((v): v is string => typeof v === "string")
              : [];

            return (
              <QuestionBlockMulti
                title={def.title()}
                subtitle={def.subtitle()}
                options={options}
                selectedKeys={selectedKeys}
                onToggle={(key) => {
                  const updated = selectedKeys.includes(key)
                    ? selectedKeys.filter((k) => k !== key)
                    : [...selectedKeys, key];
                  updateAnswer(def.key, updated);
                }}
              />
            );
          },
          isValid: (): boolean => {
            const value = answers[def.key];
            return Array.isArray(value) && value.length > 0;
          },
        });
      } else if (def.type === "form") {
        const fields = def.fields.map((field) => ({
          key: field.key,
          label: field.label(),
          placeholder: field.placeholder?.(),
          type: field.type,
        }));

        steps.push({
          key: def.key,
          render: () => {
            const formValues =
              answers[def.key] && typeof answers[def.key] === "object"
                ? (answers[def.key] as Record<string, string | boolean>)
                : {};

            return (
              <QuestionForm
                title={def.title()}
                subtitle={def.subtitle()}
                fields={fields}
                values={formValues}
                onChange={(fieldKey, value) =>
                  updateFormField(def.key, fieldKey, value)
                }
              />
            );
          },
          isValid: (): boolean => {
            const formData = answers[def.key];
            if (!formData || typeof formData !== "object") return false;
            // All fields must be filled (except boolean which are optional)
            return def.fields.every((field) => {
              const value = (formData as Record<string, string | boolean>)[field.key];
              // Boolean fields are always valid
              if (field.type === "boolean") return true;
              // For text fields, check if value exists and is not empty
              return value && typeof value === "string" && value.trim().length > 0;
            });
          },
        });
      }
    }

    return steps;
  }, [answers, updateAnswer, updateFormField, checkDisabled, checkRequires, t]);

  return useMemo(() => buildSteps(), [buildSteps]);
}
