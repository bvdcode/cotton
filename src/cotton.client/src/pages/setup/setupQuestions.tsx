import { setupBasicsQuestions } from "./setupBasicsQuestions";
import { setupServicesQuestions } from "./setupServicesQuestions";

export const setupStepDefinitions = [
  ...setupBasicsQuestions,
  ...setupServicesQuestions,
];
