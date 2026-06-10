using UnifiedMessenger.Models;
using UnifiedMessenger.Services;
using UnifiedMessenger.ViewModels;

namespace UnifiedMessenger.Tests;

public class BranchOperationalCatalogTests : IDisposable
{
    private readonly string _tempDirectory;
    private readonly string _storePath;
    private readonly List<BranchOperationalProfile> _originalCatalog;

    public BranchOperationalCatalogTests()
    {
        _tempDirectory = Path.Combine(Path.GetTempPath(), "UnifiedMessengerTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDirectory);
        _storePath = Path.Combine(_tempDirectory, "settings.json");
        _originalCatalog = CloneCatalog(AppSettingsService.Instance.Settings.BranchOperationalCatalog);
    }

    [Fact]
    public void DefaultCatalog_ContainsExpectedBranches()
    {
        var defaults = BranchOperationalCatalogDefaults.CreateDefaultList();

        Assert.Equal(3, defaults.Count);
        Assert.Contains(defaults, profile => profile.BranchKey == "DHA-2");
        Assert.Contains(defaults, profile => profile.BranchKey == "F-11");
        Assert.Contains(defaults, profile => profile.BranchKey == "Men-DHA-2");
        Assert.Contains("Bridal Makeup", defaults.Single(profile => profile.BranchKey == "DHA-2").Services);
    }

    [Fact]
    public async Task LoadAsync_MigratesVersion5SettingsWithEmptyCatalog()
    {
        const string legacyJson = """
            {
              "version": 5,
              "slaThresholdMinutes": 20,
              "branchOperationalCatalog": []
            }
            """;
        await File.WriteAllTextAsync(_storePath, legacyJson);

        var service = new AppSettingsService(_storePath);
        await service.LoadAsync();

        Assert.Equal(AppSettings.CurrentVersion, service.Settings.Version);
        Assert.Equal(3, service.Settings.BranchOperationalCatalog.Count);
        Assert.Contains(
            service.Settings.BranchOperationalCatalog,
            profile => profile.BranchKey == "DHA-2");
    }

    [Fact]
    public async Task UpdateAsync_PersistsBranchOperationalCatalog()
    {
        var service = new AppSettingsService(_storePath);
        await service.LoadAsync();

        await service.UpdateAsync(settings =>
        {
            settings.BranchOperationalCatalog =
            [
                new BranchOperationalProfile
                {
                    BranchKey = "DHA-2",
                    Services = ["Custom Bridal"],
                    StandardPackages = ["Weekend Special"],
                    BookingRules = "100% advance required."
                }
            ];
        });

        var reloaded = new AppSettingsService(_storePath);
        await reloaded.LoadAsync();

        var profile = Assert.Single(reloaded.Settings.BranchOperationalCatalog);
        Assert.Equal("DHA-2", profile.BranchKey);
        Assert.Equal(["Custom Bridal"], profile.Services);
        Assert.Equal(["Weekend Special"], profile.StandardPackages);
        Assert.Equal("100% advance required.", profile.BookingRules);

        var json = await File.ReadAllTextAsync(_storePath);
        Assert.Contains("\"branchOperationalCatalog\"", json, StringComparison.Ordinal);
        Assert.Contains("\"Custom Bridal\"", json, StringComparison.Ordinal);
    }

    [Fact]
    public void OperationalContextBuilder_UsesCatalogFromSettings()
    {
        AppSettingsService.Instance.Settings.BranchOperationalCatalog =
        [
            new BranchOperationalProfile
            {
                BranchKey = "DHA-2",
                Services = ["Test Service Alpha"],
                StandardPackages = ["Test Package Beta"],
                BookingRules = "Custom booking rule text."
            }
        ];

        var block = WhatsAppOperationalContextBuilder.BuildOperationalReferenceBlock(
            "Depilex DHA-2",
            "DHA-2",
            metadata: null,
            "120363@s.whatsapp.net",
            "wa-test");

        Assert.Contains("Test Service Alpha", block, StringComparison.Ordinal);
        Assert.Contains("Test Package Beta", block, StringComparison.Ordinal);
        Assert.Contains("Custom booking rule text.", block, StringComparison.Ordinal);
    }

    [Fact]
    public void OperationalContextBuilder_FallsBackForUnknownBranch()
    {
        AppSettingsService.Instance.Settings.BranchOperationalCatalog =
            BranchOperationalCatalogDefaults.CreateDefaultList();

        var block = WhatsAppOperationalContextBuilder.BuildOperationalReferenceBlock(
            "Depilex Gulberg",
            branchKey: "Gulberg",
            metadata: null,
            "120363@s.whatsapp.net",
            "wa-test");

        Assert.Contains("Branch: Gulberg", block, StringComparison.Ordinal);
        Assert.Contains("Consultation", block, StringComparison.Ordinal);
        Assert.Contains("Confirm date, time, and branch", block, StringComparison.Ordinal);
    }

    [Fact]
    public void SettingsPageHelper_ParsesCommaSeparatedLists()
    {
        var row = new BranchOperationalCatalogRowViewModel
        {
            BranchKey = "F-11",
            ServicesText = " Haircare , Styling , ",
            StandardPackagesText = "Combo A, Combo B",
            BookingRulesText = " Walk-ins welcome "
        };

        var profile = SettingsPageHelper.ToBranchOperationalProfile(row);

        Assert.Equal(["Haircare", "Styling"], profile.Services);
        Assert.Equal(["Combo A", "Combo B"], profile.StandardPackages);
        Assert.Equal("Walk-ins welcome", profile.BookingRules);
    }

    [Fact]
    public void Normalize_TrimsBranchCatalogEntries()
    {
        var settings = new AppSettings
        {
            Version = 6,
            BranchOperationalCatalog =
            [
                new BranchOperationalProfile
                {
                    BranchKey = " DHA-2 ",
                    Services = [" Bridal Makeup ", "Bridal Makeup"],
                    StandardPackages = [" Package A "],
                    BookingRules = " Rule text "
                }
            ]
        };

        settings.Normalize();

        var profile = Assert.Single(settings.BranchOperationalCatalog);
        Assert.Equal("DHA-2", profile.BranchKey);
        Assert.Single(profile.Services);
        Assert.Equal("Bridal Makeup", profile.Services[0]);
        Assert.Equal(["Package A"], profile.StandardPackages);
        Assert.Equal("Rule text", profile.BookingRules);
    }

    public void Dispose()
    {
        AppSettingsService.Instance.Settings.BranchOperationalCatalog = CloneCatalog(_originalCatalog);

        if (Directory.Exists(_tempDirectory))
        {
            Directory.Delete(_tempDirectory, recursive: true);
        }
    }

    private static List<BranchOperationalProfile> CloneCatalog(IEnumerable<BranchOperationalProfile> catalog) =>
        catalog.Select(profile => new BranchOperationalProfile
        {
            BranchKey = profile.BranchKey,
            Services = profile.Services.ToList(),
            StandardPackages = profile.StandardPackages.ToList(),
            BookingRules = profile.BookingRules
        }).ToList();
}
