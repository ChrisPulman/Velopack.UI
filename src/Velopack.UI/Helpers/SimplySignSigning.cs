// <copyright file="SimplySignSigning.cs" company="Chris Pulman">
// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.
// </copyright>

using System.Diagnostics;
using System.IO;
using System.Net.Mail;
using System.Runtime.Versioning;
using System.Text.RegularExpressions;

namespace Velopack.UI.Helpers;

/// <summary>
/// Configures SafeSign and preserves existing Authenticode signatures during packaging.
/// </summary>
[SupportedOSPlatform("windows10.0.19041.0")]
internal static class SimplySignSigning
{
    private const string ExecutableVariable = "VELOPACK_UI_SAFESIGN_EXECUTABLE";
    private const string ProviderVariable = "VELOPACK_UI_SAFESIGN_PROVIDER";
    private const string TimestampVariable = "VELOPACK_UI_SAFESIGN_TIMESTAMP";
    private const string FileVariable = "VELOPACK_UI_SAFESIGN_FILE";
    private static readonly string[] EnvironmentPrefixes = ["CERTUM_", "SAFESIGN_", "AWS_", "AZURE_", "GOOGLE_", "VAULT_", "OCI_", "DIGICERT_", "SSLCOM_", "CSC_", "SIGNSERVER_", "FORTANIX_", "CODESIGNSECURE_", "GARASIGN_", "SIGNPATH_", "VENAFI_"];
    private static readonly string[] ExtraPayloadExtensions = [".efi", ".msi", ".msp", ".mst", ".cab", ".cat", ".ps1", ".psm1", ".psd1", ".ps1xml", ".vbs", ".vbe", ".js", ".wsf"];

    /// <summary>Gets the SafeSign provider catalog.</summary>
    internal static IReadOnlyList<string> Providers { get; } = Array.AsReadOnly<string>(["certum", "file", "store", "aws-kms", "azure-artifact-signing", "azure-key-vault", "google-cloud-kms", "hashicorp-vault", "oracle-kms", "digicert-keylocker", "sslcom-esigner", "csc", "keyfactor-signserver", "fortanix-dsm", "encryption-consulting", "garasign", "signpath", "venafi-codesign"]);

    /// <summary>Returns a configuration error or null when SafeSign is ready.</summary>
    /// <param name="model">The signing settings.</param>
    /// <returns>The configuration error, if any.</returns>
    internal static string? GetValidationError(VelopackModel model)
    {
        var executable = model.SimplySignExecutablePath;
        if (string.IsNullOrWhiteSpace(executable) || !Path.IsPathFullyQualified(executable) ||
            !string.Equals(Path.GetExtension(executable), ".exe", StringComparison.OrdinalIgnoreCase) || !File.Exists(executable))
        {
            return "Select an installed safesign.exe using its full path.";
        }

        if (!Providers.Contains(model.SafeSignProvider, StringComparer.Ordinal))
        {
            return "Select a supported SafeSign provider.";
        }

        if (!string.IsNullOrWhiteSpace(model.SignParams) || !string.IsNullOrWhiteSpace(model.SignTemplate) || !string.IsNullOrWhiteSpace(model.AzureTrustedSignFile))
        {
            return "Clear Sign Params, Sign Template and Azure Trusted Sign File when using SafeSign.";
        }

        if (!string.IsNullOrWhiteSpace(model.SafeSignTimestampUrl) && (!Uri.TryCreate(model.SafeSignTimestampUrl, UriKind.Absolute, out var timestamp) ||
            (timestamp.Scheme != Uri.UriSchemeHttp && timestamp.Scheme != Uri.UriSchemeHttps) || !string.IsNullOrEmpty(timestamp.UserInfo)))
        {
            return "Enter an HTTP or HTTPS timestamp URL without credentials.";
        }

        if (ParseEnvironment(model.SafeSignEnvironment, out _) is { } environmentError)
        {
            return environmentError;
        }

        if (model.SafeSignProvider != "certum")
        {
            return null;
        }

        if (!string.IsNullOrWhiteSpace(model.SimplySignEmail) && (!MailAddress.TryCreate(model.SimplySignEmail, out var address) ||
                !string.Equals(address.Address, model.SimplySignEmail, StringComparison.Ordinal)))
        {
            return "Enter a valid Certum SimplySign email address, or supply it through the environment.";
        }

        return !string.IsNullOrWhiteSpace(model.SimplySignToken) &&
                (model.SimplySignToken.Length != 6 || !model.SimplySignToken.All(static character => character is >= '0' and <= '9'))
            ? "The SimplySign authenticator code must contain exactly six digits." : null;
    }

    /// <summary>Sets credentials on the packaging process and returns its signing template.</summary>
    /// <param name="startInfo">The packaging process configuration.</param>
    /// <param name="model">The signing settings.</param>
    /// <returns>The custom signing template.</returns>
    internal static string Configure(ProcessStartInfo startInfo, VelopackModel model)
    {
        if (GetValidationError(model) is { } error)
        {
            throw new InvalidOperationException(error);
        }

        var wrapper = Path.ChangeExtension(typeof(App).Assembly.Location, ".exe");
        if (string.IsNullOrWhiteSpace(wrapper))
        {
            wrapper = Environment.ProcessPath ?? throw new InvalidOperationException("Cannot locate the SafeSign packaging helper.");
        }

        if (wrapper.IndexOfAny(['"', '%', '!', '^', '&', '|', '<', '>', '\r', '\n']) >= 0)
        {
            throw new InvalidOperationException("The application path cannot contain command interpreter special characters when signing.");
        }

        _ = ParseEnvironment(model.SafeSignEnvironment, out var environment);
        foreach (var pair in environment)
        {
            startInfo.Environment[pair.Key] = pair.Value;
        }

        ApplyEnvironmentAliases(startInfo, model.SafeSignProvider, environment);

        startInfo.Environment[ExecutableVariable] = model.SimplySignExecutablePath;
        startInfo.Environment[ProviderVariable] = model.SafeSignProvider;
        startInfo.Environment[TimestampVariable] = model.SafeSignTimestampUrl ?? string.Empty;
        if (model.SafeSignProvider == "certum")
        {
            if (!string.IsNullOrWhiteSpace(model.SimplySignEmail))
            {
                startInfo.Environment["CERTUM_SIMPLYSIGN_EMAIL"] = model.SimplySignEmail;
            }

            if (!string.IsNullOrWhiteSpace(model.SimplySignToken))
            {
                foreach (var name in new[] { "CERTUM_SIMPLYSIGN_TOTP_SECRET", "CERTUM_OTP_URI", "CERTUM_TOTP_SECRET", "CERTUM_SIMPLYSIGN_REFRESH_TOKEN", "CERTUM_SIMPLYSIGN_ACCESS_TOKEN" })
                {
                    startInfo.Environment.Remove(name);
                }

                startInfo.Environment["CERTUM_SIMPLYSIGN_OTP"] = model.SimplySignToken;
            }
        }

        // The wrapper uses ArgumentList to launch SafeSign; provider credentials never enter a shell command.
        return $"\"{wrapper}\" --safesign-file {{{{file}}}}";
    }

    /// <summary>Signs payload formats that Velopack's PE-only payload scan does not visit.</summary>
    /// <param name="packDirectory">The application staging directory.</param>
    /// <param name="model">The signing settings.</param>
    /// <param name="packStartInfo">The configured packaging environment.</param>
    /// <param name="cancellationToken">Cancels signing and terminates signing processes.</param>
    /// <returns>A task representing signing the additional payload files.</returns>
    internal static async Task SignAdditionalPayloadAsync(string packDirectory, VelopackModel model, ProcessStartInfo packStartInfo, CancellationToken cancellationToken = default)
    {
        ValidateTemplatePath(packDirectory);
        ValidateTemplatePath(model.VelopackOutputPath ?? string.Empty);
        var exclude = string.IsNullOrWhiteSpace(model.SignExclude) ? null : new Regex(model.SignExclude, RegexOptions.None, TimeSpan.FromSeconds(2));
        foreach (var file in Directory.EnumerateFiles(packDirectory, "*", SearchOption.AllDirectories))
        {
            var extension = Path.GetExtension(file);
            if (string.Equals(extension, ".exe", StringComparison.OrdinalIgnoreCase) || string.Equals(extension, ".dll", StringComparison.OrdinalIgnoreCase))
            {
                ValidateTemplatePath(file);
            }

            if (ExtraPayloadExtensions.Contains(Path.GetExtension(file), StringComparer.OrdinalIgnoreCase) && !(exclude?.IsMatch(file) ?? false))
            {
                await SignFileAsync(file, packStartInfo, cancellationToken).ConfigureAwait(false);
            }
        }
    }

    /// <summary>Rejects shell expansion in paths substituted by Velopack's Windows signing template.</summary>
    /// <param name="path">The staging, output, or payload path.</param>
    internal static void ValidateTemplatePath(string path)
    {
        if (path.IndexOfAny(['%', '!', '\r', '\n']) < 0)
        {
            return;
        }

        throw new InvalidOperationException("SafeSign packaging paths and payload names cannot contain %, !, or line breaks because Velopack expands its signing template through cmd.exe. Choose another output folder or rename the payload.");
    }

    /// <summary>Signs an unsigned file and refuses to overwrite damaged existing signatures.</summary>
    /// <param name="file">The payload or installer path.</param>
    /// <param name="environment">The private signing environment.</param>
    /// <param name="cancellationToken">Cancels signing and terminates signing processes.</param>
    /// <returns>A task representing signing.</returns>
    internal static async Task SignFileAsync(string file, ProcessStartInfo environment, CancellationToken cancellationToken = default)
    {
        var probe = CreateChildProcess(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "WindowsPowerShell", "v1.0", "powershell.exe"), environment);
        probe.Environment[FileVariable] = Path.GetFullPath(file);
        foreach (var argument in new[] { "-NoProfile", "-NonInteractive", "-Command", "$ErrorActionPreference='Stop'; Import-Module (Join-Path $PSHOME 'Modules\\Microsoft.PowerShell.Security\\Microsoft.PowerShell.Security.psd1'); $s=Microsoft.PowerShell.Security\\Get-AuthenticodeSignature -LiteralPath $env:VELOPACK_UI_SAFESIGN_FILE; [Console]::Write($s.Status.ToString() + '|' + ($null -ne $s.TimeStamperCertificate).ToString())" })
        {
            probe.ArgumentList.Add(argument);
        }

        var (probeCode, status) = await RunInspectionAsync(probe, cancellationToken).ConfigureAwait(false);
        if (probeCode != 0)
        {
            throw new InvalidOperationException($"Could not inspect the signature of '{file}'. Signing was stopped.");
        }

        if (PreserveFileSignature(file, status))
        {
            return;
        }

        if (!environment.Environment.TryGetValue(ExecutableVariable, out var executable) || string.IsNullOrWhiteSpace(executable) ||
            !environment.Environment.TryGetValue(ProviderVariable, out var provider) || string.IsNullOrWhiteSpace(provider) || !Providers.Contains(provider, StringComparer.Ordinal))
        {
            throw new InvalidOperationException("The SafeSign child environment is missing or invalid. Start signing from Velopack.UI.");
        }

        ApplyEnvironmentAliases(environment, provider, null);
        var signer = CreateChildProcess(executable, environment);
        foreach (var argument in new[] { "sign", Path.GetFullPath(file), "--provider", provider, "--hash", "SHA256" })
        {
            signer.ArgumentList.Add(argument);
        }

        if (environment.Environment.TryGetValue(TimestampVariable, out var timestamp) && !string.IsNullOrWhiteSpace(timestamp))
        {
            signer.ArgumentList.Add("--timestamp-url");
            signer.ArgumentList.Add(timestamp);
        }

        AddNonSecretProviderOptions(signer, provider);

        var (exitCode, _) = await RunAsync(signer, cancellationToken).ConfigureAwait(false);
        if (exitCode != 0)
        {
            // Provider diagnostics can contain credentials. Do not copy them into the packaging log.
            throw new InvalidOperationException($"SafeSign failed for '{file}' (exit code {exitCode}). Check the selected provider's environment configuration.");
        }

        var (verifiedCode, verifiedStatus) = await RunInspectionAsync(probe, cancellationToken).ConfigureAwait(false);
        if (verifiedCode == 0 && PreserveFileSignature(file, verifiedStatus) &&
            (string.IsNullOrWhiteSpace(timestamp) || verifiedStatus.Trim().EndsWith("|True", StringComparison.Ordinal)))
        {
            return;
        }

        throw new InvalidOperationException($"SafeSign did not produce a verifiable signature and requested timestamp for '{file}'.");
    }

    /// <summary>Applies the existing-signature policy to a Windows signature status.</summary>
    /// <param name="status">The Authenticode status.</param>
    /// <returns>Whether the existing signature should be preserved.</returns>
    internal static bool PreserveSignature(string status) => status switch
    {
        "Valid" or "NotTrusted" => true,
        "NotSigned" => false,
        _ => throw new InvalidOperationException($"The file has an invalid or unsupported Authenticode signature ({status}). Signing was stopped to preserve the original file."),
    };

    private static bool PreserveFileSignature(string file, string result)
    {
        var status = result.Trim().Split('|')[0];
        if (status != "UnknownError")
        {
            return PreserveSignature(status);
        }

        // PowerShell reports untrusted roots as UnknownError on some Windows versions.
        // Use the numeric trust result instead of matching localized error messages.
        var trust = NativeMethods.VerifyAuthenticode(file);
        return trust is 0 or 0x800B0109 or 0x800B010A or 0x800B0101
            ? true : throw new InvalidOperationException($"The file has an invalid Authenticode signature (0x{trust:X8}). Signing was stopped to preserve the original file.");
    }

    private static void ApplyEnvironmentAliases(ProcessStartInfo process, string provider, Dictionary<string, string>? session)
    {
        (string Source, string Target)[] aliases = provider switch
        {
            "file" => [("SAFESIGN_FILE_PATH", "SAFESIGN_CERT_FILE"), ("SAFESIGN_FILE_PASSWORD", "SAFESIGN_CERT_PASSWORD")],
            "store" => [("SAFESIGN_STORE_THUMBPRINT", "SAFESIGN_CERT_THUMBPRINT")],
            "azure-artifact-signing" => [("AZURE_CODESIGNING_ENDPOINT", "AZURE_ARTIFACT_SIGNING_ENDPOINT"), ("AZURE_CODESIGNING_ACCOUNT", "AZURE_ARTIFACT_SIGNING_ACCOUNT"), ("AZURE_CODESIGNING_PROFILE", "AZURE_ARTIFACT_SIGNING_PROFILE"), ("AZURE_CODESIGNING_ACCESS_TOKEN", "AZURE_ARTIFACT_SIGNING_ACCESS_TOKEN")],
            "google-cloud-kms" => [("GOOGLE_OAUTH_ACCESS_TOKEN", "GOOGLE_CLOUD_KMS_ACCESS_TOKEN")],
            "hashicorp-vault" => [("VAULT_TRANSIT_KEY", "SAFESIGN_VAULT_KEY"), ("VAULT_CERTIFICATE_PATH", "SAFESIGN_CERT_FILE")],
            "oracle-kms" => [("OCI_KMS_CERTIFICATE_PATH", "SAFESIGN_CERT_FILE"), ("OCI_TENANCY_OCID", "OCI_CLI_TENANCY"), ("OCI_USER_OCID", "OCI_CLI_USER"), ("OCI_FINGERPRINT", "OCI_CLI_FINGERPRINT"), ("OCI_PRIVATE_KEY_PATH", "OCI_CLI_KEY_FILE")],
            "keyfactor-signserver" => [("SIGNSERVER_ENDPOINT", "SAFESIGN_SIGNSERVER_URL"), ("SIGNSERVER_WORKER", "SAFESIGN_SIGNSERVER_WORKER"), ("SIGNSERVER_CERTIFICATE_PATH", "SAFESIGN_CERT_FILE"), ("SIGNSERVER_ACCESS_TOKEN", "SAFESIGN_SIGNSERVER_TOKEN")],
            "signpath" => [("SIGNPATH_SIGNING_POLICY_SLUG", "SIGNPATH_POLICY_SLUG"), ("SIGNPATH_CERTIFICATE_PATH", "SIGNPATH_CERT_FILE")],
            _ => [],
        };
        foreach (var (source, target) in aliases)
        {
            if (session?.TryGetValue(source, out var explicitValue) == true && !session.ContainsKey(target))
            {
                process.Environment[target] = explicitValue;
            }
            else if (!process.Environment.ContainsKey(target) && process.Environment.TryGetValue(source, out var value))
            {
                process.Environment[target] = value;
            }
        }
    }

    private static void AddNonSecretProviderOptions(ProcessStartInfo signer, string provider)
    {
        // SafeSign 1.1.0 has no environment fallback for these public identifiers.
        // Secrets are deliberately excluded: those continue through native environment fallbacks.
        (string Variable, string Option)[] options = provider switch
        {
            "aws-kms" => [("AWS_KMS_KEY_ID", "key-id"), ("AWS_KMS_CERTIFICATE_PATH", "cert-file")],
            "google-cloud-kms" => [("GOOGLE_KMS_KEY_VERSION", "key-version-name"), ("GOOGLE_KMS_CERTIFICATE_PATH", "cert-file")],
            "azure-key-vault" => [("AZURE_KEYVAULT_URL", "vault-uri"), ("AZURE_KEYVAULT_CERTIFICATE_NAME", "cert-name"), ("AZURE_KEYVAULT_CERTIFICATE_VERSION", "cert-version")],
            _ => [],
        };
        foreach (var (variable, option) in options)
        {
            if (signer.Environment.TryGetValue(variable, out var value) && !string.IsNullOrWhiteSpace(value))
            {
                signer.ArgumentList.Add("--provider-option");
                signer.ArgumentList.Add(option + "=" + value);
            }
        }
    }

    private static async Task<(int ExitCode, string Output)> RunInspectionAsync(ProcessStartInfo probe, CancellationToken cancellationToken)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(30));
        try
        {
            return await RunAsync(probe, timeout.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new TimeoutException("Windows signature inspection exceeded 30 seconds. Signing was stopped.");
        }
    }

    private static string? ParseEnvironment(string? text, out Dictionary<string, string> environment)
    {
        environment = new(StringComparer.OrdinalIgnoreCase);
        foreach (var line in (text ?? string.Empty).Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries))
        {
            var separator = line.IndexOf('=');
            var name = separator < 0 ? string.Empty : line[..separator].Trim();
            if (separator < 1 || !Regex.IsMatch(name, "^[A-Z][A-Z0-9_]*$", RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1)) ||
                !EnvironmentPrefixes.Any(prefix => name.StartsWith(prefix, StringComparison.Ordinal)) || environment.ContainsKey(name) || line.Contains('\0'))
            {
                return "Enter one unique SafeSign provider environment variable per line as NAME=value (for example SAFESIGN_FILE_PASSWORD=...).";
            }

            environment.Add(name, line[(separator + 1)..]);
        }

        return null;
    }

    private static ProcessStartInfo CreateChildProcess(string executable, ProcessStartInfo environment)
    {
        var child = new ProcessStartInfo(executable) { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true };
        child.Environment.Clear();
        foreach (var pair in environment.Environment)
        {
            child.Environment[pair.Key] = pair.Value;
        }

        return child;
    }

    private static async Task<(int ExitCode, string Output)> RunAsync(ProcessStartInfo startInfo, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        using var process = Process.Start(startInfo) ?? throw new InvalidOperationException("Could not start a signing process.");
        var output = process.StandardOutput.ReadToEndAsync(CancellationToken.None);
        var error = process.StandardError.ReadToEndAsync(CancellationToken.None);
        try
        {
            await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
            }

            await process.WaitForExitAsync(CancellationToken.None).ConfigureAwait(false);
            throw;
        }

        await error.ConfigureAwait(false);
        return (process.ExitCode, await output.ConfigureAwait(false));
    }
}
