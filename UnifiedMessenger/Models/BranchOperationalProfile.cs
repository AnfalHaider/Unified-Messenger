namespace UnifiedMessenger.Models;

public sealed class BranchOperationalProfile
{
    public string BranchKey { get; set; } = string.Empty;

    public List<string> Services { get; set; } = [];

    public List<string> StandardPackages { get; set; } = [];

    public string BookingRules { get; set; } = string.Empty;
}

public static class BranchOperationalCatalogDefaults
{
    public static List<BranchOperationalProfile> CreateDefaultList() =>
    [
        new()
        {
            BranchKey = "DHA-2",
            Services =
            [
                "Bridal Makeup",
                "Party Makeup",
                "Hair Styling",
                "Hair Treatment",
                "Nails",
                "Skincare"
            ],
            StandardPackages =
            [
                "Bridal Full Day",
                "Party Glam",
                "Hair Spa + Blowdry"
            ],
            BookingRules = "Bookings require 50% advance for bridal; party slots 3+ days ahead."
        },
        new()
        {
            BranchKey = "F-11",
            Services =
            [
                "Haircare",
                "Styling",
                "Treatment",
                "Nails",
                "Threading"
            ],
            StandardPackages =
            [
                "Haircut + Blowdry",
                "Keratin Treatment",
                "Mani-Pedi Combo"
            ],
            BookingRules = "Walk-ins accepted until 6 PM; peak Saturday requires pre-booking."
        },
        new()
        {
            BranchKey = "Men-DHA-2",
            Services =
            [
                "Haircut",
                "Beard Trim",
                "Facial",
                "Hair Color"
            ],
            StandardPackages =
            [
                "Executive Grooming",
                "Beard Styling Package"
            ],
            BookingRules = "Same-day slots limited after 4 PM."
        }
    ];

    public static BranchOperationalProfile CreateFallbackProfile(string branchKey) =>
        new()
        {
            BranchKey = branchKey,
            Services = ["Haircare", "Styling", "Treatment", "Nails", "Bridal Makeup"],
            StandardPackages = ["Consultation", "Custom package on request"],
            BookingRules = "Confirm date, time, and branch before quoting final price."
        };
}
