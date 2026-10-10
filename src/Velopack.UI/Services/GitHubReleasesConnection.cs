// <copyright file="GitHubReleasesConnection.cs" company="Chris Pulman">
// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.
// </copyright>

using System.Runtime.Serialization;
using System.Runtime.Versioning;
using System.Text.Json.Serialization;
using FluentValidation;
using FluentValidation.Results;
using ReactiveUI;
using ReactiveUI.Primitives;
using ReactiveUI.SourceGenerators;

namespace Velopack.UI;

/// <summary>
/// Publishes artifacts to GitHub Releases.
/// GitHub CLI credentials stay in its credential store; fallback tokens are session-only.
/// </summary>
[DataContract]
[SupportedOSPlatform("windows10.0.19041.0")]
public partial class GitHubReleasesConnection : WebConnectionBase
{
    private string? packageChannel = "win";
    private string? packageTitle;
    private string? repositoryUrl;
    private bool isApplyingRepositoryUrl;

    [DataMember]
    [Reactive]
    private string? owner;

    [DataMember]
    [Reactive]
    private string? repository;

    [DataMember]
    [Reactive]
    private string? tagName;

    [DataMember]
    [Reactive]
    private string? releaseName;

    [DataMember]
    [Reactive]
    private bool prerelease;

    [DataMember]
    [Reactive]
    private bool draft;

    [DataMember]
    [Reactive]
    private bool useGitHubCliAuthentication = true;

    /// <summary>
    /// Optional fallback token used only for the current session.
    /// </summary>
    [Reactive]
    [JsonIgnore]
    private string? token;

    /// <summary>
    /// Initializes a new instance of the <see cref="GitHubReleasesConnection"/> class.
    /// </summary>
    public GitHubReleasesConnection()
    {
        this.ConnectionName = "GitHub Releases";
        this.WhenAnyValue(x => x.Owner, x => x.Repository)
            .Subscribe(_ =>
            {
                if (!this.isApplyingRepositoryUrl)
                {
                    this.repositoryUrl = null;
                    this.RaisePropertyChanged(nameof(this.RepositoryUrl));
                }

                this.RefreshDownloadUrls();
            });
        this.WhenAnyValue(x => x.TagName)
            .Subscribe(_ => this.RefreshDownloadUrls());
    }

    /// <summary>
    /// Gets or sets the package channel used to build the setup asset filename.
    /// </summary>
    [JsonIgnore]
    public string? PackageChannel
    {
        get => this.packageChannel;

        set
        {
            this.RaiseAndSetIfChanged(ref this.packageChannel, value);
            this.RefreshDownloadUrls();
        }
    }

    /// <summary>
    /// Gets or sets the package title used to build the setup asset filename.
    /// </summary>
    [JsonIgnore]
    public string? PackageTitle
    {
        get => this.packageTitle;

        set
        {
            this.RaiseAndSetIfChanged(ref this.packageTitle, value);
            this.RefreshDownloadUrls();
        }
    }

    /// <summary>
    /// Gets the permanent repository URL to pass to Velopack's GithubSource.
    /// </summary>
    [JsonIgnore]
    public string UpdateSourceUrl =>
        string.IsNullOrWhiteSpace(this.Owner) || string.IsNullOrWhiteSpace(this.Repository)
            ? "Missing Parameter"
            : $"https://github.com/{this.Owner}/{this.Repository}";

    /// <summary>
    /// Gets the setup link that follows GitHub's latest stable release.
    /// </summary>
    [JsonIgnore]
    public string LatestSetupDownloadUrl => this.UpdateSourceUrl == "Missing Parameter"
        ? "Missing Parameter"
        : $"{this.UpdateSourceUrl}/releases/latest/download/{Uri.EscapeDataString(this.GetSetupFileName())}";

    /// <summary>
    /// Gets or sets a GitHub repository URL to parse into owner and repository.
    /// </summary>
    [JsonIgnore]
    public string? RepositoryUrl
    {
        get => this.repositoryUrl ?? (string.IsNullOrWhiteSpace(this.Owner) || string.IsNullOrWhiteSpace(this.Repository)
            ? null
            : $"https://github.com/{this.Owner}/{this.Repository}");

        set
        {
            this.RaiseAndSetIfChanged(ref this.repositoryUrl, value);
            this.isApplyingRepositoryUrl = true;
            try
            {
                this.TryApplyRepositoryUrl(value);
            }
            finally
            {
                this.isApplyingRepositoryUrl = false;
            }
        }
    }

    /// <summary>
    /// Gets example download URL for Setup.exe in this release.
    /// </summary>
    public string SetupDownloadUrl
    {
        get
        {
            if (string.IsNullOrWhiteSpace(this.Owner) || string.IsNullOrWhiteSpace(this.Repository) || string.IsNullOrWhiteSpace(this.TagName))
            {
                return "Missing Parameter";
            }

            return $"{this.UpdateSourceUrl}/releases/download/{Uri.EscapeDataString(this.TagName)}/{Uri.EscapeDataString(this.GetSetupFileName())}";
        }
    }

    /// <summary>
    /// Validates this instance.
    /// </summary>
    /// <returns>A ValidationResult.</returns>
    public override ValidationResult Validate()
    {
        this.RefreshDownloadUrls();
        var commonValid = new Validator().Validate(this);
        if (!commonValid.IsValid)
        {
            return commonValid;
        }

        return base.Validate();
    }

    private void TryApplyRepositoryUrl(string? value)
    {
        if (string.IsNullOrWhiteSpace(value) || !Uri.TryCreate(value, UriKind.Absolute, out var uri))
        {
            return;
        }

        if (!string.Equals(uri.Host, "github.com", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        var segments = uri.AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length < 2)
        {
            return;
        }

        this.Owner = segments[0];
        this.Repository = segments[1].EndsWith(".git", StringComparison.OrdinalIgnoreCase)
            ? segments[1][..^4]
            : segments[1];

        if (segments.Length >= 5 &&
            string.Equals(segments[2], "releases", StringComparison.OrdinalIgnoreCase) &&
            string.Equals(segments[3], "tag", StringComparison.OrdinalIgnoreCase))
        {
            this.TagName = Uri.UnescapeDataString(segments[4]);
        }

        this.RefreshDownloadUrls();
    }

    private string GetSetupFileName()
    {
        var title = string.IsNullOrWhiteSpace(this.PackageTitle) ? this.Repository : this.PackageTitle;
        var channel = string.IsNullOrWhiteSpace(this.PackageChannel) ? "win" : this.PackageChannel;
        return $"{title}-{channel}-Setup.exe";
    }

    private void RefreshDownloadUrls()
    {
        this.RaisePropertyChanged(nameof(this.RepositoryUrl));
        this.RaisePropertyChanged(nameof(this.UpdateSourceUrl));
        this.RaisePropertyChanged(nameof(this.LatestSetupDownloadUrl));
        this.RaisePropertyChanged(nameof(this.SetupDownloadUrl));
    }

    private class Validator : AbstractValidator<GitHubReleasesConnection>
    {
        public Validator()
        {
            this.RuleFor(c => c.Owner).NotEmpty();
            this.RuleFor(c => c.Repository).NotEmpty();
            this.RuleFor(c => c.TagName).NotEmpty();
            this.RuleFor(c => c.Token).NotEmpty().When(c => !c.UseGitHubCliAuthentication);
        }
    }
}
