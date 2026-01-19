import { httpClient } from "./httpClient";

export interface TotpSetup {
  secretBase32: string;
  otpAuthUri: string;
}

export interface ConfirmTotpRequest {
  twoFactorCode: string;
}

export const totpApi = {
  setup: async (): Promise<TotpSetup> => {
    const response = await httpClient.post<TotpSetup>("auth/totp/setup", {});
    return response.data;
  },

  confirm: async (twoFactorCode: string): Promise<void> => {
    const request: ConfirmTotpRequest = { twoFactorCode };
    await httpClient.post("auth/totp/confirm", request);
  },
};
