// Copyright (c) Chris Pulman. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Reactive.Linq;
using System.Runtime.Serialization;
using System.Runtime.Versioning;
using System.Text.Json.Serialization;
using FluentValidation;
using FluentValidation.Results;
using ReactiveUI;
using ReactiveUI.SourceGenerators;

namespace Velopack.UI;

/// <summary>
/// Publishes artifacts to GitHub Releases.
/// Credentials are stored in plain text in the project file.
/// </summary>
[DataContract]
[SupportedOSPlatform("windows10.0.19041.0")]
public partial class GitHubReleasesConnection : WebConnectionBase
{
    private string? _packageChannel = "win";
    private string? _packageTitle;
    private string? _repositoryUrl;

    [DataMember]
    [Reactive]
    private string? _owner;

    [DataMember]
    [Reactive]
    private string? _repository;

    [DataMember]
    [Reactive]
    private string? _tagName;

    [DataMember]
    [Reactive]
    private string? _releaseName;

    [DataMember]
    [Reactive]
    private bool _prerelease;

    [DataMember]
    [Reactive]
    private bool _draft;

    [Reactive]
    [JsonIgnore]
    private string? _token; // GitHub PAT with repo scope

    /// <summary>
    /// Initializes a new instance of the <see cref="GitHubReleasesConnection"/> class.
    /// </summary>
    public GitHubReleasesConnection()
    {
        ConnectionName = "GitHub Releases";
        this.WhenAnyValue(x => x.Owner, x => x.Repository, x => x.TagName)
            .Subscribe(_ => this.RaisePropertyChanged(nameof(SetupDownloadUrl)));
    }

    /// <summary>
    /// Gets or sets the package channel used to build the setup asset filename.
    /// </summary>
    [JsonIgnore]
    public string? PackageChannel
    {
        get => _packageChannel;

        set
        {
            this.RaiseAndSetIfChanged(ref _packageChannel, value);
            this.RaisePropertyChanged(nameof(SetupDownloadUrl));
        }
    }

    /// <summary>
    /// Gets or sets the package title used to build the setup asset filename.
    /// </summary>
    [JsonIgnore]
    public string? PackageTitle
    {
        get => _packageTitle;

        set
        {
            this.RaiseAndSetIfChanged(ref _packageTitle, value);
            this.RaisePropertyChanged(nameof(SetupDownloadUrl));
        }
    }

    /// <summary>
    /// Gets or sets a GitHub repository URL to parse into owner and repository.
    /// </summary>
    [JsonIgnore]
    public string? RepositoryUrl
    {
        get => _repositoryUrl;

        set
        {
            this.RaiseAndSetIfChanged(ref _repositoryUrl, value);
            TryApplyRepositoryUrl(value);
        }
    }

    /// <summary>
    /// Gets example download URL for Setup.exe in this release.
    /// </summary>
    public string SetupDownloadUrl
    {
        get
        {
            if (string.IsNullOrWhiteSpace(Owner) || string.IsNullOrWhiteSpace(Repository) || string.IsNullOrWhiteSpace(TagName))
            {
                return "Missing Parameter";
            }

            var packageTitle = string.IsNullOrWhiteSpace(PackageTitle) ? Repository : PackageTitle;
            var packageChannel = string.IsNullOrWhiteSpace(PackageChannel) ? "win" : PackageChannel;
            var setupFileName = $"{packageTitle}-{packageChannel}-Setup.exe";

            return $"https://github.com/{Owner}/{Repository}/releases/download/{Uri.EscapeDataString(TagName)}/{Uri.EscapeDataString(setupFileName)}";
        }
    }

    /// <summary>
    /// Validates this instance.
    /// </summary>
    /// <returns>A ValidationResult.</returns>
    public override ValidationResult Validate()
    {
        this.RaisePropertyChanged(nameof(SetupDownloadUrl));
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

        Owner = segments[0];
        Repository = segments[1].EndsWith(".git", StringComparison.OrdinalIgnoreCase)
            ? segments[1][..^4]
            : segments[1];

        if (segments.Length >= 5 &&
            string.Equals(segments[2], "releases", StringComparison.OrdinalIgnoreCase) &&
            string.Equals(segments[3], "tag", StringComparison.OrdinalIgnoreCase))
        {
            TagName = Uri.UnescapeDataString(segments[4]);
        }

        this.RaisePropertyChanged(nameof(SetupDownloadUrl));
    }

    private class Validator : AbstractValidator<GitHubReleasesConnection>
    {
        public Validator()
        {
            RuleFor(c => c.Owner).NotEmpty();
            RuleFor(c => c.Repository).NotEmpty();
            RuleFor(c => c.TagName).NotEmpty();
            RuleFor(c => c.Token).NotEmpty();
        }
    }
}
