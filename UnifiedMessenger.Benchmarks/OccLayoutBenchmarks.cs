using BenchmarkDotNet.Attributes;
using UnifiedMessenger.Models;
using UnifiedMessenger.Services;

namespace UnifiedMessenger.Benchmarks;

[MemoryDiagnoser]
public class OccLayoutBenchmarks
{
    private AppSettings _settings = null!;

    [GlobalSetup]
    public void Setup()
    {
        _settings = new AppSettings();
        OccLayoutService.ApplyDefaults(_settings);
    }

    [Benchmark]
    public IReadOnlyList<OccPanelPlacement> ResolveLayout() =>
        OccLayoutGridEngine.Resolve(_settings);

    [Benchmark]
    public IReadOnlyList<OccPanelPlacement> ReflowAt960() =>
        OccLayoutGridEngine.ReflowForWidth(OccLayoutGridEngine.Resolve(_settings), 960);
}
