using System.Runtime.Serialization;
using System.Runtime.Versioning;
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

    [DataMember]
    [Reactive]
    private string? _token; // GitHub PAT with repo scope

    public GitHubReleasesConnection() => ConnectionName = "GitHub Releases";

    /// <summary>
    /// Example download URL for Setup.exe in this release.
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
