using System.Runtime.Serialization;
using System.Runtime.Versioning;
using FluentValidation;
using FluentValidation.Results;
using Microsoft.Win32;
using ReactiveUI;
using ReactiveUI.SourceGenerators;

namespace Velopack.UI;

/// <summary>
/// This class contains all information about WebConncetion uploading. Information for user :
/// Credentials are stored in clear format.
/// </summary>
[DataContract]
[SupportedOSPlatform("windows10.0.19041.0")]
public partial class FileSystemConnection : WebConnectionBase
{
    private string? _fileSystemPath;

    /// <summary>
    /// Initializes a new instance of the <see cref="FileSystemConnection"/> class.
    /// </summary>
    public FileSystemConnection() => ConnectionName = "File System";

    /// <summary>
    /// Gets or sets the file system path.
    /// </summary>
    /// <value>The file system path.</value>
    [DataMember]
    public string? FileSystemPath
    {
        get => _fileSystemPath;

        set
        {
            this.RaiseAndSetIfChanged(ref _fileSystemPath, value);
            this.RaisePropertyChanged(nameof(SetupDownloadUrl));
        }
    }

    /// <summary>
    /// Gets the setup download URL.
    /// </summary>
    /// <value>The setup download URL.</value>
    public string SetupDownloadUrl
    {
        get
        {
            if (string.IsNullOrWhiteSpace(FileSystemPath)) {
                return "Missing Parameter";
            }

            return System.IO.Path.Combine(FileSystemPath, "Setup.exe");
        }
    }

    /// <summary>
    /// Prima controllo correttezza del pattern poi controllo questo.
    /// </summary>
    /// <returns></returns>
    public override ValidationResult Validate()
    {
        var commonValid = new Validator().Validate(this);
        if (!commonValid.IsValid) {
            return commonValid;
        }

        return base.Validate();
    }

    [ReactiveCommand]
    private void SelectFolder()
    {
        // show Directory Picker
        var dialog = new OpenFolderDialog
        {
            Title = "Select a folder for your Application Installer",
            InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
            Multiselect = false
        };

        if (dialog.ShowDialog() == true)
        {
            FileSystemPath = dialog.FolderName;
        }
    }

    private class Validator : AbstractValidator<FileSystemConnection>
    {
        public Validator() => RuleFor(c => c.FileSystemPath).NotEmpty();
    }
}
