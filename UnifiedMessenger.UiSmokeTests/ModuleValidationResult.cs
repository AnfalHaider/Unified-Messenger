namespace UnifiedMessenger.UiSmokeTests;

internal enum ModuleValidationSeverity
{
    Pass,
    Warn,
    Fail
}

internal sealed class ModuleValidationResult
{
    public required string Module { get; init; }

    public required string Layer { get; init; }

    public bool Passed { get; init; }

    public bool IsWarning { get; init; }

    public ModuleValidationSeverity Severity { get; init; }

    public string Detail { get; init; } = string.Empty;

    public static ModuleValidationResult Pass(string module, string layer, string detail) =>
        new()
        {
            Module = module,
            Layer = layer,
            Passed = true,
            IsWarning = false,
            Severity = ModuleValidationSeverity.Pass,
            Detail = detail
        };

    public static ModuleValidationResult Fail(string module, string layer, string detail) =>
        new()
        {
            Module = module,
            Layer = layer,
            Passed = false,
            IsWarning = false,
            Severity = ModuleValidationSeverity.Fail,
            Detail = detail
        };

    public static ModuleValidationResult Warn(string module, string layer, string detail) =>
        new()
        {
            Module = module,
            Layer = layer,
            Passed = true,
            IsWarning = true,
            Severity = ModuleValidationSeverity.Warn,
            Detail = detail
        };
}
