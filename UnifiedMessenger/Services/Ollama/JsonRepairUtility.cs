using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace UnifiedMessenger.Services.Ollama;

/// <summary>
/// Repairs markdown-wrapped, chatty, or truncated JSON from local LLM streams
/// before <see cref="JsonSerializer"/> deserialization.
/// </summary>
public static partial class JsonRepairUtility
{
    private static readonly JsonSerializerOptions DeserializeOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        AllowTrailingCommas = true,
        ReadCommentHandling = JsonCommentHandling.Skip
    };

    public static bool TryDeserialize<T>(string? raw, out T? value) where T : class
    {
        value = null;
        if (string.IsNullOrWhiteSpace(raw))
        {
            return false;
        }

        if (!TryExtractJsonObject(raw, out var json))
        {
            return false;
        }

        try
        {
            value = JsonSerializer.Deserialize<T>(json, DeserializeOptions);
            return value is not null;
        }
        catch (JsonException)
        {
            if (!TryCloseTruncatedJson(json, out var repaired))
            {
                return false;
            }

            try
            {
                value = JsonSerializer.Deserialize<T>(repaired, DeserializeOptions);
                return value is not null;
            }
            catch (JsonException)
            {
                return false;
            }
        }
    }

    public static string? TryExtractJsonObject(string raw) =>
        TryExtractJsonObject(raw, out var json) ? json : null;

    public static bool TryExtractJsonObject(string raw, out string json)
    {
        json = string.Empty;
        if (string.IsNullOrWhiteSpace(raw))
        {
            return false;
        }

        var normalized = StripMarkdownFences(raw.Trim());
        var start = normalized.IndexOf('{');
        if (start < 0)
        {
            return false;
        }

        var slice = normalized[start..];
        if (TryBalanceBrackets(slice, out var balanced))
        {
            json = balanced;
            return true;
        }

        if (TryCloseTruncatedJson(slice, out var closed))
        {
            json = closed;
            return true;
        }

        return false;
    }

    private static string StripMarkdownFences(string input)
    {
        var trimmed = input.Trim();
        var fenceMatch = MarkdownFenceRegex().Match(trimmed);
        if (!fenceMatch.Success)
        {
            return trimmed;
        }

        var body = fenceMatch.Groups["body"].Value.Trim();
        return string.IsNullOrWhiteSpace(body) ? trimmed : body;
    }

    private static bool TryBalanceBrackets(string input, out string balanced)
    {
        balanced = string.Empty;
        var stack = new Stack<char>();
        var inString = false;
        var escaped = false;

        for (var i = 0; i < input.Length; i++)
        {
            var ch = input[i];

            if (inString)
            {
                if (escaped)
                {
                    escaped = false;
                    continue;
                }

                if (ch == '\\')
                {
                    escaped = true;
                    continue;
                }

                if (ch == '"')
                {
                    inString = false;
                }

                continue;
            }

            if (ch == '"')
            {
                inString = true;
                continue;
            }

            if (ch is '{' or '[')
            {
                stack.Push(ch);
                continue;
            }

            if (ch is '}' or ']')
            {
                if (stack.Count == 0)
                {
                    balanced = input[..(i + 1)];
                    return true;
                }

                var open = stack.Pop();
                if ((open == '{' && ch != '}') || (open == '[' && ch != ']'))
                {
                    return false;
                }

                if (stack.Count == 0)
                {
                    balanced = input[..(i + 1)];
                    return true;
                }
            }
        }

        return false;
    }

    private static bool TryCloseTruncatedJson(string input, out string repaired)
    {
        repaired = string.Empty;
        if (string.IsNullOrWhiteSpace(input) || input.IndexOf('{') < 0)
        {
            return false;
        }

        var sb = new StringBuilder(input);
        var stack = new Stack<char>();
        var inString = false;
        var escaped = false;

        foreach (var ch in input)
        {
            if (inString)
            {
                if (escaped)
                {
                    escaped = false;
                    continue;
                }

                if (ch == '\\')
                {
                    escaped = true;
                    continue;
                }

                if (ch == '"')
                {
                    inString = false;
                }

                continue;
            }

            if (ch == '"')
            {
                inString = true;
                continue;
            }

            if (ch is '{' or '[')
            {
                stack.Push(ch);
                continue;
            }

            if (ch is '}' or ']')
            {
                if (stack.Count > 0)
                {
                    stack.Pop();
                }
            }
        }

        if (inString)
        {
            sb.Append('"');
        }

        TrimTrailingComma(sb);

        while (stack.Count > 0)
        {
            sb.Append(stack.Pop() switch
            {
                '{' => '}',
                '[' => ']',
                _ => '}'
            });
        }

        repaired = sb.ToString();
        return repaired.IndexOf('{') >= 0;
    }

    private static void TrimTrailingComma(StringBuilder sb)
    {
        var i = sb.Length - 1;
        while (i >= 0 && char.IsWhiteSpace(sb[i]))
        {
            i--;
        }

        if (i >= 0 && sb[i] == ',')
        {
            sb.Remove(i, 1);
        }
    }

    [GeneratedRegex(@"```(?:json)?\s*(?<body>[\s\S]*?)```", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex MarkdownFenceRegex();
}
