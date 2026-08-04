// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

const datacenterCities: Readonly<Record<string, string>> = {
  AMS: "Amsterdam",
  ARN: "Stockholm",
  ATL: "Atlanta",
  BKK: "Bangkok",
  BOM: "Mumbai",
  BOS: "Boston",
  BRU: "Brussels",
  CDG: "Paris",
  CPH: "Copenhagen",
  DFW: "Dallas",
  DME: "Moscow",
  DOH: "Doha",
  DUB: "Dublin",
  DXB: "Dubai",
  EWR: "Newark",
  FRA: "Frankfurt",
  GRU: "São Paulo",
  HEL: "Helsinki",
  HKG: "Hong Kong",
  IAD: "Ashburn",
  ICN: "Seoul",
  JNB: "Johannesburg",
  KIX: "Osaka",
  LAX: "Los Angeles",
  LED: "Saint Petersburg",
  LHR: "London",
  LIS: "Lisbon",
  MAD: "Madrid",
  MIA: "Miami",
  MNL: "Manila",
  MRS: "Marseille",
  MUC: "Munich",
  NRT: "Tokyo",
  ORD: "Chicago",
  OTP: "Bucharest",
  PRG: "Prague",
  SEA: "Seattle",
  SIN: "Singapore",
  SJC: "San Jose",
  SYD: "Sydney",
  TLV: "Tel Aviv",
  VIE: "Vienna",
  WAW: "Warsaw",
  YUL: "Montreal",
  YYZ: "Toronto",
  ZRH: "Zurich",
};

const countryFlag = (countryCode: string): string =>
  String.fromCodePoint(
    ...[...countryCode].map((character) => character.charCodeAt(0) + 127397),
  );

export const formatCloudflareCountry = (
  countryCode: string | null,
  language: string,
): string | null => {
  if (!countryCode) return null;
  if (countryCode === "T1") return "Tor (T1)";
  if (countryCode === "XX") return "XX";

  let displayName: string | undefined;
  try {
    displayName = new Intl.DisplayNames([language], { type: "region" }).of(
      countryCode,
    );
  } catch {
    displayName = undefined;
  }

  return `${countryFlag(countryCode)} ${displayName ?? countryCode}`;
};

export const formatCloudflareDatacenter = (
  datacenterCode: string | null,
): string | null => {
  if (!datacenterCode) return null;
  const city = datacenterCities[datacenterCode];
  return city ? `${city} (${datacenterCode})` : datacenterCode;
};
