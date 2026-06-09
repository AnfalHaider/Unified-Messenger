using System.Reflection;
using UnifiedMessenger.Controls;
using UnifiedMessenger.Pages;
using UnifiedMessenger.Services;
using Windows.System;
using Xunit;

namespace UnifiedMessenger.Tests;

public class DeferredItemsTests
{
    [Fact]
    public void OperationsCommandCenter_MainCodeBehind_UnderLineTarget()
    {
        var repoRoot = FindRepoRoot();
        var lines = ProgramCompletionCriteria.CountLines(Path.Combine(
            repoRoot,
            "UnifiedMessenger",
            "Controls",
            "OperationsCommandCenter.xaml.cs"));

        Assert.True(lines < ProgramCompletionCriteria.OccCodeBehindLineTarget);
    }

    [Fact]
    public void ChartBarRenderHelper_ExistsInOccNamespace()
    {
        var assembly = typeof(OperationsCommandCenter).Assembly;
        var helper = assembly.GetType("UnifiedMessenger.Controls.Occ.ChartBarRenderHelper", throwOnError: false);

        Assert.NotNull(helper);
        Assert.True(helper!.IsClass && helper.IsAbstract && helper.IsSealed);
    }

    [Fact]
    public void RegistryNavigationArgs_ExposesApplicationServices()
    {
        var property = typeof(RegistryNavigationArgs).GetProperty(
            nameof(RegistryNavigationArgs.Services),
            BindingFlags.Instance | BindingFlags.Public);

        Assert.NotNull(property);
        Assert.Equal(typeof(ApplicationServices), property!.PropertyType);
    }

    [Fact]
    public void PageServices_CreatesRegistryArgsWithServices()
    {
        var services = new ApplicationServices();
        var args = PageServices.CreateRegistryArgs(services);

        Assert.Same(services.Registry, args.Registry);
        Assert.Same(services, args.Services);
    }

    [Fact]
    public void OperationsCommandCenter_IsKeyboardReorderKey_RequiresAltArrow()
    {
        Assert.False(OperationsCommandCenter.IsKeyboardReorderKey(VirtualKey.Up, isAltDown: false));
        Assert.False(OperationsCommandCenter.IsKeyboardReorderKey(VirtualKey.Left, isAltDown: true));
        Assert.True(OperationsCommandCenter.IsKeyboardReorderKey(VirtualKey.Up, isAltDown: true));
        Assert.True(OperationsCommandCenter.IsKeyboardReorderKey(VirtualKey.Down, isAltDown: true));
    }

    private static string FindRepoRoot()
    {
        var dir = AppContext.BaseDirectory;
        while (!string.IsNullOrEmpty(dir))
        {
            if (File.Exists(Path.Combine(dir, "UnifiedMessenger.sln")))
            {
                return dir;
            }

            dir = Directory.GetParent(dir)?.FullName ?? string.Empty;
        }

        return Directory.GetCurrentDirectory();
    }
}
