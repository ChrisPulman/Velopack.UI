// <copyright file="SimplySignSigning.cs" company="Chris Pulman">
// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.
// </copyright>

using System.Diagnostics;
using System.IO;
using System.Net.Mail;
using System.Runtime.Versioning;

namespace Velopack.UI.Helpers;

/// <summary>
/// Configures the ssign custom signing command and its private child environment.
/// </summary>
[SupportedOSPlatform("windows10.0.19041.0")]
internal static class SimplySignSigning
{
    /// <summary>
    /// Returns a configuration error or null when SimplySign is ready.
    /// </summary>
    /// <param name="model">The signing settings.</param>
    /// <returns>The configuration error, if any.</returns>
    internal static string? GetValidationError(VelopackModel model)
    {
        var executable = model.SimplySignExecutablePath;
        if (string.IsNullOrWhiteSpace(executable) || !Path.IsPathFullyQualified(executable) ||
            !string.Equals(Path.GetExtension(executable), ".exe", StringComparison.OrdinalIgnoreCase) || !File.Exists(executable))
        {
            return "Select an installed ssign.exe using its full path.";
        }

        // Velopack executes signing templates through a command interpreter on Windows.
        if (executable.IndexOfAny(['"', '%', '!', '^', '&', '|', '<', '>', '\r', '\n']) >= 0)
        {
            return "The ssign executable path cannot contain command interpreter special characters.";
        }

        if (string.IsNullOrWhiteSpace(model.SimplySignEmail) || !MailAddress.TryCreate(model.SimplySignEmail, out var address) ||
            !string.Equals(address.Address, model.SimplySignEmail, StringComparison.Ordinal))
        {
            return "Enter the SimplySign account email address.";
        }

        if (model.GenerateMsi || model.MsiDeploymentTool)
        {
            return "ssign supports PE binaries only. Disable MSI generation and MSI Deployment Tool to use SimplySign.";
        }

        if (!string.IsNullOrWhiteSpace(model.SignParams) || !string.IsNullOrWhiteSpace(model.SignTemplate) ||
            !string.IsNullOrWhiteSpace(model.AzureTrustedSignFile))
        {
            return "Clear Sign Params, Sign Template and Azure Trusted Sign File when using SimplySign.";
        }

        var token = model.SimplySignToken;
        if (!string.IsNullOrWhiteSpace(token))
        {
            return token.Length == 6 && token.All(static character => character is >= '0' and <= '9')
                ? null : "The SimplySign authenticator code must contain exactly six digits.";
        }

        var seed = Environment.GetEnvironmentVariable("CERTUM_OTP");
        var environmentToken = Environment.GetEnvironmentVariable("CERTUM_TOKEN");
        if (string.IsNullOrWhiteSpace(seed) && string.IsNullOrWhiteSpace(environmentToken))
        {
            return "Enter the current six-digit authenticator code, or set CERTUM_OTP or CERTUM_TOKEN before starting the app.";
        }

        if (!string.IsNullOrWhiteSpace(seed) && !string.IsNullOrWhiteSpace(environmentToken))
        {
            return "Set only one of CERTUM_OTP and CERTUM_TOKEN, or enter a current authenticator code.";
        }

        if (!string.IsNullOrWhiteSpace(environmentToken) &&
            (environmentToken.Length != 6 || !environmentToken.All(static character => character is >= '0' and <= '9')))
        {
            return "CERTUM_TOKEN must contain exactly six digits.";
        }

        return null;
    }

    /// <summary>
    /// Sets credentials on the packaging process and returns its signing template.
    /// </summary>
    /// <param name="startInfo">The packaging process configuration.</param>
    /// <param name="model">The signing settings.</param>
    /// <returns>The custom signing template.</returns>
    internal static string Configure(ProcessStartInfo startInfo, VelopackModel model)
    {
        if (GetValidationError(model) is { } error)
        {
            throw new InvalidOperationException(error);
        }

        startInfo.Environment["CERTUM_EMAIL"] = model.SimplySignEmail;
        if (!string.IsNullOrWhiteSpace(model.SimplySignToken))
        {
            startInfo.Environment.Remove("CERTUM_OTP");
            startInfo.Environment["CERTUM_TOKEN"] = model.SimplySignToken;
        }

        // Credentials are inherited by ssign; none enter the command line or project file.
        return $"\"{model.SimplySignExecutablePath}\" {{{{file}}}}";
    }
}
