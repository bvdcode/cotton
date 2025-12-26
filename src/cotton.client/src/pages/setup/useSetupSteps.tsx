import { useCallback, useMemo, type ReactNode } from "react";
import { setupStepDefinitions } from "./setupQuestions.tsx";
import {
  QuestionBlock,
  QuestionBlockMulti,
  QuestionForm,
} from "./components";

type BuiltStep = {
  key: string;
  render: () => ReactNode;
  isValid: () => boolean;
};

export function useSetupSteps(
  answers: Record<string, unknown>,
  updateAnswer: (key: string, value: unknown) => void,
  updateFormField: (stepKey: string, fieldKey: string, value: string) => void,
) {
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
        const options = optionsList.map((opt) => ({
          key: opt.key,
          label: opt.label(),
          description: opt.description?.(),
          value: opt.value,
          icon: opt.icon,
        }));

        steps.push({
          key: def.key,
          render: () => {
            const selectedKey =
              typeof answers[def.key] === "string"
                ? (answers[def.key] as string)
                : null;

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
        const options = def.options.map((opt) => ({
          key: opt.key,
          label: opt.label(),
          description: opt.description?.(),
          icon: opt.icon,
        }));

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
                ? (answers[def.key] as Record<string, string>)
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
            // All fields must be filled
            return def.fields.every((field) => {
              const value = (formData as Record<string, string>)[field.key];
              return value && value.trim().length > 0;
            });
          },
        });
      }
    }

    return steps;
  }, [answers, updateAnswer, updateFormField]);

  return useMemo(() => buildSteps(), [buildSteps]);
}
