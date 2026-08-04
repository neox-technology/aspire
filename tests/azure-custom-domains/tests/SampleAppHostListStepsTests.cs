using System.Diagnostics;
using Xunit;

namespace Neox.Aspire.Hosting.Azure.CustomDomains.Tests;

public sealed class SampleAppHostListStepsTests
{
    [Fact]
    public void SampleAppHost_ProjectFile_Exists()
    {
        var path = FindSampleAppHostProject();
        Assert.True(File.Exists(path), $"Expected sample AppHost at {path}");
    }

    [Fact]
    public async Task SampleAppHost_BuildsSuccessfully()
    {
        var project = FindSampleAppHostProject();
        var startInfo = new ProcessStartInfo
        {
            FileName = "dotnet",
            ArgumentList = { "build", project, "-c", "Release", "--nologo", "-v", "q" },
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false
        };

        using var process = Process.Start(startInfo)
            ?? throw new InvalidOperationException("Failed to start dotnet build.");

        var stdout = await process.StandardOutput.ReadToEndAsync();
        var stderr = await process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();

        Assert.True(process.ExitCode == 0, $"Build failed:{Environment.NewLine}{stdout}{Environment.NewLine}{stderr}");
    }

    private static string FindSampleAppHostProject()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            var candidate = Path.Combine(
                dir.FullName,
                "tests",
                "azure-custom-domains",
                "sample-apphost",
                "Neox.Aspire.Hosting.Azure.CustomDomains.Tests.SampleAppHost.csproj");

            if (File.Exists(candidate))
            {
                return candidate;
            }

            // When running from artifacts/bin/.../net10.0, walk up to repo root.
            candidate = Path.Combine(
                dir.FullName,
                "sample-apphost",
                "Neox.Aspire.Hosting.Azure.CustomDomains.Tests.SampleAppHost.csproj");
            if (File.Exists(candidate))
            {
                return candidate;
            }

            dir = dir.Parent;
        }

        return Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory,
            "..", "..", "..", "..", "..", "..",
            "tests", "azure-custom-domains", "sample-apphost",
            "Neox.Aspire.Hosting.Azure.CustomDomains.Tests.SampleAppHost.csproj"));
    }
}
