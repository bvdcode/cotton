// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Benchmark.Models;

namespace Cotton.Benchmark.Infrastructure
{
    internal static class BenchmarkOptionParser
    {
        public static BenchmarkOptions Parse(string[] args)
        {
            BenchmarkMode mode = BenchmarkMode.Machine;
            BenchmarkProfile profile = BenchmarkProfile.Standard;
            bool list = false;
            bool compare = false;
            bool? update = null;
            string baselineDirectory = BenchmarkPathDefaults.BaselineDirectory;
            string resultsDirectory = BenchmarkPathDefaults.ResultsDirectory;
            int? compressionLevel = null;
            var scenarioFilters = new List<string>();

            for (int i = 0; i < args.Length; i++)
            {
                string arg = args[i];
                switch (arg)
                {
                    case "--help":
                    case "-h":
                        return new BenchmarkOptions { ShowHelp = true };
                    case "--mode":
                        mode = ParseEnumValue<BenchmarkMode>(ReadValue(args, ref i, arg), arg);
                        break;
                    case "--profile":
                        profile = ParseEnumValue<BenchmarkProfile>(ReadValue(args, ref i, arg), arg);
                        break;
                    case "--scenario":
                        scenarioFilters.AddRange(SplitFilters(ReadValue(args, ref i, arg)));
                        break;
                    case "--list":
                        list = true;
                        break;
                    case "--compare":
                    case "--compare-baseline":
                        compare = true;
                        break;
                    case "--update-baseline":
                        update = true;
                        break;
                    case "--no-update-baseline":
                        update = false;
                        break;
                    case "--baseline-dir":
                        baselineDirectory = ReadValue(args, ref i, arg);
                        break;
                    case "--results-dir":
                        resultsDirectory = ReadValue(args, ref i, arg);
                        break;
                    case "--compression-level":
                        compressionLevel = ParseIntValue(ReadValue(args, ref i, arg), arg);
                        break;
                    default:
                        throw new ArgumentException($"Unknown benchmark option: {arg}");
                }
            }

            return new BenchmarkOptions
            {
                Mode = mode,
                Profile = profile,
                ListBenchmarks = list,
                CompareBaseline = compare,
                UpdateBaseline = update ?? ShouldUpdateBaselineByDefault(list, compare, scenarioFilters),
                BaselineDirectory = baselineDirectory,
                ResultsDirectory = resultsDirectory,
                CompressionLevel = compressionLevel,
                ScenarioFilters = scenarioFilters
            };
        }

        private static bool ShouldUpdateBaselineByDefault(
            bool list,
            bool compare,
            IReadOnlyCollection<string> scenarioFilters)
        {
            return !list
                && !compare
                && scenarioFilters.Count == 0;
        }

        private static int ParseIntValue(string value, string optionName)
        {
            if (int.TryParse(value, out int parsed))
            {
                return parsed;
            }

            throw new ArgumentException($"Invalid {optionName} value '{value}'. Expected an integer.");
        }

        private static TEnum ParseEnumValue<TEnum>(string value, string optionName)
            where TEnum : struct, Enum
        {
            if (Enum.TryParse(value, ignoreCase: true, out TEnum parsed))
            {
                return parsed;
            }

            string supportedValues = string.Join(", ", Enum.GetNames<TEnum>().Select(x => x.ToLowerInvariant()));
            throw new ArgumentException($"Invalid {optionName} value '{value}'. Supported values: {supportedValues}.");
        }

        private static string ReadValue(string[] args, ref int index, string optionName)
        {
            if (index + 1 >= args.Length)
            {
                throw new ArgumentException($"Missing value for {optionName}.");
            }

            index++;
            return args[index];
        }

        private static IEnumerable<string> SplitFilters(string value)
        {
            return value
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Where(x => !string.IsNullOrWhiteSpace(x));
        }
    }
}
