import { httpClient } from "./httpClient";

export interface TotpSetup {
  secretBase32: string;
  otpAuthUri: string;
}

export interface ConfirmTotpRequest {
  twoFactorCode: string;
}

export interface DisableTotpRequest {
  password: string;
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

  disable: async (password: string): Promise<void> => {
    const request: DisableTotpRequest = { password };
    await httpClient.delete("auth/totp/disable", { data: request });
  },
};
