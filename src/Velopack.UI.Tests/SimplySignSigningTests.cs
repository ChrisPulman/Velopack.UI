// <copyright file="SimplySignSigningTests.cs" company="Chris Pulman">
// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.
// </copyright>

using System.Diagnostics;
using System.IO;
using System.Text.Json;
using Velopack.UI.Helpers;

namespace Velopack.UI.Tests;

/// <summary>
/// Verifies application behavior and input validation.
/// </summary>
public sealed class SimplySignSigningTests
{
    /// <summary>
    /// Verifies credentials are limited to the signing child process environment.
    /// </summary>
    /// <returns>A task that completes after the assertions.</returns>
    [Test]
    public async Task Configure_ManualToken_UsesChildEnvironmentAndQuotedExecutable()
    {
        var directory = CreateDirectory("Signing Tools");
        try
        {
            var model = CreateModel(directory);
            var startInfo = new ProcessStartInfo();
            startInfo.Environment["CERTUM_OTP"] = "inherited seed";
            var parentToken = Environment.GetEnvironmentVariable("CERTUM_TOKEN");
            var parentEmail = Environment.GetEnvironmentVariable("CERTUM_EMAIL");

            var template = SimplySignSigning.Configure(startInfo, model);

            await Assert.That(template).IsEqualTo($"\"{model.SimplySignExecutablePath}\" {{{{file}}}}");
            await Assert.That(template.Contains(model.SimplySignToken!, StringComparison.Ordinal)).IsFalse();
            await Assert.That(startInfo.Environment["CERTUM_EMAIL"]).IsEqualTo(model.SimplySignEmail);
            await Assert.That(startInfo.Environment["CERTUM_TOKEN"]).IsEqualTo(model.SimplySignToken);
            await Assert.That(startInfo.Environment.ContainsKey("CERTUM_OTP")).IsFalse();
            await Assert.That(Environment.GetEnvironmentVariable("CERTUM_TOKEN")).IsEqualTo(parentToken);
            await Assert.That(Environment.GetEnvironmentVariable("CERTUM_EMAIL")).IsEqualTo(parentEmail);
        }
        finally
        {
            Directory.Delete(directory, true);
        }
    }

    /// <summary>
    /// Verifies command interpreter characters cannot enter executable paths.
    /// </summary>
    /// <param name="character">The unsafe path character.</param>
    /// <returns>A task that completes after the assertions.</returns>
    [Test]
    [Arguments("&")]
    [Arguments("%")]
    [Arguments("!")]
    [Arguments("^")]
    public async Task GetValidationError_CommandInterpreterCharacter_RejectsExecutable(string character)
    {
        var directory = CreateDirectory("Signing" + character + "Tools");
        try
        {
            var model = CreateModel(directory);

            await Assert.That(SimplySignSigning.GetValidationError(model))
                .IsEqualTo("The ssign executable path cannot contain command interpreter special characters.");
        }
        finally
        {
            Directory.Delete(directory, true);
        }
    }

    /// <summary>
    /// Verifies only six ASCII digits are accepted as authenticator codes.
    /// </summary>
    /// <param name="token">The invalid authenticator code.</param>
    /// <returns>A task that completes after the assertions.</returns>
    [Test]
    [Arguments("12345")]
    [Arguments("1234567")]
    [Arguments("12345a")]
    [Arguments("１２３４５６")]
    public async Task GetValidationError_InvalidAuthenticatorCode_RequiresSixAsciiDigits(string token)
    {
        var directory = CreateDirectory("Signing Tools");
        try
        {
            var model = CreateModel(directory);
            model.SimplySignToken = token;

            await Assert.That(SimplySignSigning.GetValidationError(model))
                .IsEqualTo("The SimplySign authenticator code must contain exactly six digits.");
        }
        finally
        {
            Directory.Delete(directory, true);
        }
    }

    /// <summary>
    /// Verifies MSI signing is rejected by the PE-only signer.
    /// </summary>
    /// <returns>A task that completes after the assertions.</returns>
    [Test]
    public async Task GetValidationError_MsiEnabled_RejectsUnsupportedSigning()
    {
        var directory = CreateDirectory("Signing Tools");
        try
        {
            var model = CreateModel(directory);
            model.GenerateMsi = true;

            await Assert.That(SimplySignSigning.GetValidationError(model))
                .IsEqualTo("ssign supports PE binaries only. Disable MSI generation and MSI Deployment Tool to use SimplySign.");
        }
        finally
        {
            Directory.Delete(directory, true);
        }
    }

    /// <summary>
    /// Verifies existing signing modes cannot conflict with SimplySign.
    /// </summary>
    /// <returns>A task that completes after the assertions.</returns>
    [Test]
    public async Task GetValidationError_OtherSigningConfigured_RejectsConflictingModes()
    {
        var directory = CreateDirectory("Signing Tools");
        try
        {
            var model = CreateModel(directory);
            model.SignTemplate = "other signing tool {{file}}";

            await Assert.That(SimplySignSigning.GetValidationError(model))
                .IsEqualTo("Clear Sign Params, Sign Template and Azure Trusted Sign File when using SimplySign.");
        }
        finally
        {
            Directory.Delete(directory, true);
        }
    }

    /// <summary>
    /// Verifies saved project JSON excludes the session authenticator code.
    /// </summary>
    /// <returns>A task that completes after the assertions.</returns>
    [Test]
    public async Task Serialize_Project_ExcludesSessionAuthenticatorCode()
    {
        var model = new VelopackModel { SimplySignToken = "123456", SimplySignEmail = "test@example.com" };

        var json = JsonSerializer.Serialize(model);

        await Assert.That(json.Contains("SimplySignToken", StringComparison.Ordinal)).IsFalse();
        await Assert.That(json.Contains("123456", StringComparison.Ordinal)).IsFalse();
        await Assert.That(json.Contains("test@example.com", StringComparison.Ordinal)).IsTrue();
    }

    private static string CreateDirectory(string name)
    {
        var directory = Path.Combine(Path.GetTempPath(), name + "-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        return directory;
    }

    private static VelopackModel CreateModel(string directory)
    {
        var executable = Path.Combine(directory, "ssign.exe");
        File.WriteAllText(executable, string.Empty);
        return new VelopackModel
        {
            UseSimplySign = true,
            SimplySignExecutablePath = executable,
            SimplySignEmail = "test@example.com",
            SimplySignToken = "123456",
        };
    }
}
