// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

namespace Cotton.Sync.Cli;

internal sealed class OptionReader
{
    private readonly Dictionary<string, string> _options;

    private OptionReader(Dictionary<string, string> options, List<string> positionals)
    {
        _options = options;
        Positionals = positionals;
    }

    public IReadOnlyList<string> Positionals { get; }

    public static OptionReader Parse(IEnumerable<string> args)
    {
        string[] tokens = args.ToArray();
        var options = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var positionals = new List<string>();
        for (int index = 0; index < tokens.Length; index++)
        {
            string token = tokens[index];
            if (!token.StartsWith("--", StringComparison.Ordinal))
            {
                positionals.Add(token);
                continue;
            }

            string option = token[2..];
            string value = "true";
            int equalsIndex = option.IndexOf('=', StringComparison.Ordinal);
            if (equalsIndex >= 0)
            {
                value = option[(equalsIndex + 1)..];
                option = option[..equalsIndex];
            }
            else if (index + 1 < tokens.Length && !tokens[index + 1].StartsWith("--", StringComparison.Ordinal))
            {
                value = tokens[++index];
            }

            if (string.IsNullOrWhiteSpace(option))
            {
                throw new InvalidOperationException("Option name cannot be empty.");
            }

            options[option] = value;
        }

        return new OptionReader(options, positionals);
    }

    public string GetRequired(string name)
    {
        string? value = GetOptional(name);
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidOperationException("Missing required option --" + name + ".");
        }

        return value.Trim();
    }

    public string? GetOptional(string name)
    {
        return _options.TryGetValue(name, out string? value) ? value : null;
    }

    public bool HasFlag(string name)
    {
        return _options.TryGetValue(name, out string? value)
            && bool.TryParse(value, out bool parsed)
            && parsed;
    }

    public int GetInt32(string name, int defaultValue)
    {
        string? value = GetOptional(name);
        if (string.IsNullOrWhiteSpace(value))
        {
            return defaultValue;
        }

        if (!int.TryParse(value, System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out int parsed)
            || parsed <= 0)
        {
            throw new InvalidOperationException("Option --" + name + " must be a positive integer.");
        }

        return parsed;
    }
}
