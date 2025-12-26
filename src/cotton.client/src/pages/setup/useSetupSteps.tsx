import { useCallback, useMemo, type ReactNode } from "react";
import { useTranslation } from "react-i18next";
import { setupStepDefinitions } from "./setupQuestions.tsx";
import {
  QuestionBlock,
  QuestionBlockMulti,
  QuestionForm,
  QuestionDropdown,
} from "./components";

type BuiltStep = {
  key: string;
  render: () => ReactNode;
  isValid: () => boolean;
};

export function useSetupSteps(
  answers: Record<string, unknown>,
  updateAnswer: (key: string, value: unknown) => void,
  updateFormField: (stepKey: string, fieldKey: string, value: string | boolean) => void,
) {
  const { t } = useTranslation();
  
  // Helper function to check if requirement is met
  const checkRequires = useCallback((requires?: string): boolean => {
    if (!requires) return true;
    
    const [reqKey, reqValue] = requires.split(":");
    const currentValue = answers[reqKey];
    
    // Check if it's an array (multi-select)
    if (Array.isArray(currentValue)) {
      return currentValue.includes(reqValue);
    }
    return currentValue === reqValue;
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
        const options = optionsList
          .filter((opt) => checkRequires(opt.requires))
          .map((opt) => {
          const { disabled, reasons } = checkDisabled(opt.disabledIfAny);
          const disabledTooltip = disabled && reasons.length > 0
            ? `${t("setup:questions.telemetry.disabledTooltip")} ${reasons.join(", ")}`
            : undefined;
          
          return {
            key: opt.key,
            label: opt.label(),
            description: opt.description?.(),
            value: opt.value,
            icon: opt.icon,
            disabled,
            disabledTooltip,
          };
        });

        steps.push({
          key: def.key,
          render: () => {
            // Get selected key, or use default value if not set
            let selectedKey: string | null = null;
            
            if (answers[def.key] !== undefined && typeof answers[def.key] === "string") {
              selectedKey = answers[def.key] as string;
            } else if (def.getDefaultValue && answers[def.key] === undefined) {
              // Set default value on first render
              const defaultValue = def.getDefaultValue();
              const defaultOption = options.find(opt => opt.value === defaultValue);
              if (defaultOption) {
                selectedKey = defaultOption.key;
                updateAnswer(def.key, selectedKey);
              }
            }

            // Render as dropdown or cards based on renderAs field
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
        const options = def.options
          .filter((opt) => checkRequires(opt.requires))
          .map((opt) => {
          const { disabled, reasons } = checkDisabled(opt.disabledIfAny);
          const disabledTooltip = disabled && reasons.length > 0
            ? `${t("setup:questions.telemetry.disabledTooltip")} ${reasons.join(", ")}`
            : undefined;
          
          return {
            key: opt.key,
            label: opt.label(),
            description: opt.description?.(),
            icon: opt.icon,
            disabled,
            disabledTooltip,
          };
        });

        steps.push({
          key: def.key,
          render: () => {
            const selectedKeys = Array.isArray(answers[def.key])
              ? (answers[def.key] as string[])
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
