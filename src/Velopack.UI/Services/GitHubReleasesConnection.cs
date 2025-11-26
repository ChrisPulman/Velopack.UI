// Copyright (c) Chris Pulman. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

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
    public GitHubReleasesConnection() => ConnectionName = "GitHub Releases";

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

            return $"https://github.com/{Owner}/{Repository}/releases/download/{TagName}/Setup.exe";
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
