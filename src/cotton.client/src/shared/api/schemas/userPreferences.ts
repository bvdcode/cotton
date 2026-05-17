import { z } from "zod";

export const userPreferencesSchema = z.record(z.string(), z.string());

export type UserPreferences = z.infer<typeof userPreferencesSchema>;
