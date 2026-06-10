namespace UnifiedMessenger.UiSmokeTests;

internal sealed class ModuleValidationResult
{
    public required string Module { get; init; }

    public required string Layer { get; init; }

    public bool Passed { get; init; }

    public string Detail { get; init; } = string.Empty;

    public static ModuleValidationResult Pass(string module, string layer, string detail) =>
        new() { Module = module, Layer = layer, Passed = true, Detail = detail };

    public static ModuleValidationResult Fail(string module, string layer, string detail) =>
        new() { Module = module, Layer = layer, Passed = false, Detail = detail };

    public static ModuleValidationResult Warn(string module, string layer, string detail) =>
        new() { Module = module, Layer = layer, Passed = false, Detail = $"WARN: {detail}" };
}
