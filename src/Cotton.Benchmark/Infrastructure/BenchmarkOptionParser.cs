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
            bool update = false;
            string baselineDirectory = Path.Combine("performance", "baselines");
            string resultsDirectory = Path.Combine("performance", "results");
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
                    case "--baseline-dir":
                        baselineDirectory = ReadValue(args, ref i, arg);
                        break;
                    case "--results-dir":
                        resultsDirectory = ReadValue(args, ref i, arg);
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
                UpdateBaseline = update,
                BaselineDirectory = baselineDirectory,
                ResultsDirectory = resultsDirectory,
                ScenarioFilters = scenarioFilters
            };
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
