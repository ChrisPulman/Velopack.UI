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

/// <summary>Verifies SafeSign configuration, secret handling, and signature preservation.</summary>
public sealed class SimplySignSigningTests
{
    /// <summary>Verifies credentials stay in the child environment rather than the shell template.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task Configure_ManualToken_UsesSafeSignChildEnvironment()
    {
        var directory = CreateDirectory();
        try
        {
            var model = CreateModel(directory);
            model.SafeSignEnvironment = "CERTUM_SIMPLYSIGN_REFRESH_TOKEN=private-refresh-token";
            var startInfo = new ProcessStartInfo();
            startInfo.Environment["CERTUM_SIMPLYSIGN_TOTP_SECRET"] = "private-seed";
            startInfo.Environment["CERTUM_TOTP_SECRET"] = "private-alias-seed";
            var parentToken = Environment.GetEnvironmentVariable("CERTUM_SIMPLYSIGN_OTP");

            var template = SimplySignSigning.Configure(startInfo, model);

            await Assert.That(template.EndsWith(" --safesign-file {{file}}", StringComparison.Ordinal)).IsTrue();
            await Assert.That(template.Contains(model.SimplySignToken!, StringComparison.Ordinal)).IsFalse();
            await Assert.That(template.Contains(model.SimplySignEmail!, StringComparison.Ordinal)).IsFalse();
            await Assert.That(startInfo.Environment["CERTUM_SIMPLYSIGN_EMAIL"]).IsEqualTo(model.SimplySignEmail);
            await Assert.That(startInfo.Environment["CERTUM_SIMPLYSIGN_OTP"]).IsEqualTo(model.SimplySignToken);
            await Assert.That(startInfo.Environment.ContainsKey("CERTUM_SIMPLYSIGN_TOTP_SECRET")).IsFalse();
            await Assert.That(startInfo.Environment.ContainsKey("CERTUM_TOTP_SECRET")).IsFalse();
            await Assert.That(startInfo.Environment.ContainsKey("CERTUM_SIMPLYSIGN_REFRESH_TOKEN")).IsFalse();
            await Assert.That(Environment.GetEnvironmentVariable("CERTUM_SIMPLYSIGN_OTP")).IsEqualTo(parentToken);
        }
        finally
        {
            Directory.Delete(directory, true);
        }
    }

    /// <summary>Verifies generic providers support MSI and environment credentials.</summary>
    /// <param name="provider">The provider identifier.</param>
    /// <param name="environment">The provider's session environment.</param>
    /// <param name="name">The credential variable to inspect.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments("file", "SAFESIGN_FILE_PASSWORD=private=password", "SAFESIGN_FILE_PASSWORD")]
    [Arguments("store", "SAFESIGN_STORE_THUMBPRINT=012345", "SAFESIGN_STORE_THUMBPRINT")]
    [Arguments("azure-artifact-signing", "AZURE_CLIENT_SECRET=private=password", "AZURE_CLIENT_SECRET")]
    [Arguments("azure-artifact-signing", "AZURE_CODESIGNING_ACCESS_TOKEN=private=password", "AZURE_CODESIGNING_ACCESS_TOKEN")]
    [Arguments("aws-kms", "AWS_SECRET_ACCESS_KEY=private=password", "AWS_SECRET_ACCESS_KEY")]
    public async Task Configure_GenericProvider_PassesEnvironmentWithoutCertum(string provider, string environment, string name)
    {
        var directory = CreateDirectory();
        try
        {
            var model = CreateModel(directory);
            model.SafeSignProvider = provider;
            model.SafeSignEnvironment = environment;
            model.GenerateMsi = true;
            model.MsiDeploymentTool = true;
            model.SimplySignEmail = "invalid unused email";
            model.SimplySignToken = "invalid unused token";
            var startInfo = new ProcessStartInfo();
            startInfo.Environment.Remove("CERTUM_SIMPLYSIGN_EMAIL");
            startInfo.Environment.Remove("CERTUM_SIMPLYSIGN_OTP");
            startInfo.Environment["SAFESIGN_CERT_PASSWORD"] = "inherited-password";

            var template = SimplySignSigning.Configure(startInfo, model);

            await Assert.That(SimplySignSigning.GetValidationError(model)).IsNull();
            await Assert.That(startInfo.Environment[name]).IsEqualTo(environment[(environment.IndexOf('=') + 1)..]);
            var nativeName = name switch
            {
                "SAFESIGN_FILE_PASSWORD" => "SAFESIGN_CERT_PASSWORD",
                "SAFESIGN_STORE_THUMBPRINT" => "SAFESIGN_CERT_THUMBPRINT",
                "AZURE_CODESIGNING_ACCESS_TOKEN" => "AZURE_ARTIFACT_SIGNING_ACCESS_TOKEN",
                _ => name,
            };
            await Assert.That(startInfo.Environment[nativeName]).IsEqualTo(environment[(environment.IndexOf('=') + 1)..]);
            await Assert.That(startInfo.Environment["VELOPACK_UI_SAFESIGN_PROVIDER"]).IsEqualTo(provider);
            await Assert.That(startInfo.Environment.ContainsKey("CERTUM_SIMPLYSIGN_EMAIL")).IsFalse();
            await Assert.That(startInfo.Environment.ContainsKey("CERTUM_SIMPLYSIGN_OTP")).IsFalse();
            await Assert.That(template.Contains("private=password", StringComparison.Ordinal)).IsFalse();
        }
        finally
        {
            Directory.Delete(directory, true);
        }
    }

    /// <summary>Verifies malformed or unrelated environment variables cannot alter child execution.</summary>
    /// <param name="environment">The invalid environment text.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments("PATH=unsafe")]
    [Arguments("SAFESIGN_FILE_PASSWORD")]
    [Arguments("SAFESIGN_FILE_PASSWORD=one\nSAFESIGN_FILE_PASSWORD=two")]
    [Arguments("SAFESIGN_FILE_PASSWORD=one\0two")]
    public async Task GetValidationError_InvalidEnvironment_RejectsConfiguration(string environment)
    {
        var directory = CreateDirectory();
        try
        {
            var model = CreateModel(directory);
            model.SafeSignEnvironment = environment;
            await Assert.That(SimplySignSigning.GetValidationError(model)).IsNotNull();
        }
        finally
        {
            Directory.Delete(directory, true);
        }
    }

    /// <summary>Verifies only six ASCII digits are accepted as manual authenticator codes.</summary>
    /// <param name="token">The invalid authenticator code.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments("12345")]
    [Arguments("1234567")]
    [Arguments("12345a")]
    [Arguments("１２３４５６")]
    public async Task GetValidationError_InvalidAuthenticatorCode_RequiresSixAsciiDigits(string token)
    {
        var directory = CreateDirectory();
        try
        {
            var model = CreateModel(directory);
            model.SimplySignToken = token;
            await Assert.That(SimplySignSigning.GetValidationError(model)).IsEqualTo("The SimplySign authenticator code must contain exactly six digits.");
        }
        finally
        {
            Directory.Delete(directory, true);
        }
    }

    /// <summary>Verifies existing signing modes cannot conflict with SafeSign.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task GetValidationError_OtherSigningConfigured_RejectsConflictingModes()
    {
        var directory = CreateDirectory();
        try
        {
            var model = CreateModel(directory);
            model.SignTemplate = "other signing tool {{file}}";
            await Assert.That(SimplySignSigning.GetValidationError(model)).IsEqualTo("Clear Sign Params, Sign Template and Azure Trusted Sign File when using SafeSign.");
        }
        finally
        {
            Directory.Delete(directory, true);
        }
    }

    /// <summary>Verifies unsupported providers and credential-bearing timestamp URLs are rejected.</summary>
    /// <param name="provider">The provider identifier.</param>
    /// <param name="timestamp">The timestamp URL.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments("unknown-provider", "https://timestamp.example.com")]
    [Arguments("certum", "https://user:secret@timestamp.example.com")]
    [Arguments("certum", "file:///C:/timestamp")]
    public async Task GetValidationError_InvalidProviderOrTimestamp_RejectsConfiguration(string provider, string timestamp)
    {
        var directory = CreateDirectory();
        try
        {
            var model = CreateModel(directory);
            model.SafeSignProvider = provider;
            model.SafeSignTimestampUrl = timestamp;
            await Assert.That(SimplySignSigning.GetValidationError(model)).IsNotNull();
        }
        finally
        {
            Directory.Delete(directory, true);
        }
    }

    /// <summary>Verifies valid and untrusted signatures are preserved and unsigned files are signed.</summary>
    /// <param name="status">The Windows signature status.</param>
    /// <param name="preserve">Whether to preserve the existing file.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments("Valid", true)]
    [Arguments("NotTrusted", true)]
    [Arguments("NotSigned", false)]
    public async Task PreserveSignature_KnownStatus_UsesSignaturePolicy(string status, bool preserve) =>
        await Assert.That(SimplySignSigning.PreserveSignature(status)).IsEqualTo(preserve);

    /// <summary>Verifies damaged and unsupported signatures stop signing without replacing files.</summary>
    /// <param name="status">The Windows signature error.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments("HashMismatch")]
    [Arguments("UnknownError")]
    [Arguments("NotSupportedFileFormat")]
    public async Task PreserveSignature_InvalidSignature_StopsSigning(string status) =>
        await Assert.That(() => SimplySignSigning.PreserveSignature(status)).Throws<InvalidOperationException>();

    /// <summary>Verifies Windows signed payloads are left byte-for-byte intact without signing credentials.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task SignFileAsync_SignedWindowsBinary_PreservesOriginal()
    {
        var file = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "kernel32.dll");
        var original = await File.ReadAllBytesAsync(file);
        await SimplySignSigning.SignFileAsync(file, new ProcessStartInfo());
        var after = await File.ReadAllBytesAsync(file);
        await Assert.That(after.SequenceEqual(original)).IsTrue();
    }

    /// <summary>Verifies cancellation stops before launching the signature inspector.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task SignFileAsync_Canceled_StopsBeforeStartingProcesses() =>
        await Assert.That(() => SimplySignSigning.SignFileAsync("unused.exe", new ProcessStartInfo(), new CancellationToken(true))).Throws<OperationCanceledException>();

    /// <summary>Verifies payload names cannot expand environment variables inside Velopack's shell template.</summary>
    /// <param name="path">The unsafe packaging path.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments("C:\\payload\\%TEMP%.dll")]
    [Arguments("C:\\output!\\app.exe")]
    public async Task ValidateTemplatePath_ShellExpansion_RejectsPath(string path) =>
        await Assert.That(() => SimplySignSigning.ValidateTemplatePath(path)).Throws<InvalidOperationException>();

    /// <summary>Verifies helper mode rejects missing files before starting the desktop UI.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task SigningCli_MissingArgument_ExitsWithoutOpeningDesktop()
    {
        var executable = Path.ChangeExtension(typeof(App).Assembly.Location, ".exe");
        using var child = Process.Start(new ProcessStartInfo(executable, "--safesign-file") { UseShellExecute = false, CreateNoWindow = true });
        await child!.WaitForExitAsync();
        await Assert.That(child.ExitCode).IsEqualTo(1);
    }

    /// <summary>Verifies project JSON excludes every session credential while retaining provider settings.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task Serialize_Project_ExcludesSessionCredentials()
    {
        var model = new VelopackModel { SimplySignToken = "123456", SimplySignEmail = "test@example.com", SafeSignProvider = "file", SafeSignEnvironment = "SAFESIGN_FILE_PASSWORD=private-password" };
        var json = JsonSerializer.Serialize(model);
        await Assert.That(json.Contains("SimplySignToken", StringComparison.Ordinal)).IsFalse();
        await Assert.That(json.Contains("123456", StringComparison.Ordinal)).IsFalse();
        await Assert.That(json.Contains("SafeSignEnvironment", StringComparison.Ordinal)).IsFalse();
        await Assert.That(json.Contains("private-password", StringComparison.Ordinal)).IsFalse();
        await Assert.That(json.Contains("test@example.com", StringComparison.Ordinal)).IsTrue();
        await Assert.That(json.Contains("SafeSignProvider", StringComparison.Ordinal)).IsTrue();
    }

    private static string CreateDirectory()
    {
        var directory = Path.Combine(Path.GetTempPath(), "SafeSign Tools-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        return directory;
    }

    private static VelopackModel CreateModel(string directory)
    {
        var executable = Path.Combine(directory, "safesign.exe");
        File.WriteAllText(executable, string.Empty);
        return new VelopackModel { UseSimplySign = true, SimplySignExecutablePath = executable, SimplySignEmail = "test@example.com", SimplySignToken = "123456" };
    }
}
