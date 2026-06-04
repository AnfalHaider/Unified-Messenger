using UnifiedMessenger.Services;

namespace UnifiedMessenger.Tests;

public class WindowsAppRuntimeBootstrapHelperTests
{
    [Fact]
    public void IsSelfContainedDeployment_ReturnsFalse_WhenRuntimeConfigMissing()
    {
        var directory = Path.Combine(Path.GetTempPath(), "um-bootstrap-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);

        try
        {
            Assert.False(WindowsAppRuntimeBootstrapHelper.IsSelfContainedDeployment(directory));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void IsSelfContainedDeployment_ReturnsTrue_WhenIncludedFrameworksPresent()
    {
        var directory = Path.Combine(Path.GetTempPath(), "um-bootstrap-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);

        File.WriteAllText(
            Path.Combine(directory, "UnifiedMessenger.runtimeconfig.json"),
            """
            {
              "runtimeOptions": {
                "includedFrameworks": [
                  { "name": "Microsoft.NETCore.App", "version": "8.0.27" }
                ]
              }
            }
            """);

        try
        {
            Assert.True(WindowsAppRuntimeBootstrapHelper.IsSelfContainedDeployment(directory));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }
}
