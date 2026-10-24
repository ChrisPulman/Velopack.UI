// <copyright file="VelopackModel.cs" company="Chris Pulman">
// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.
// </copyright>

using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Net.Cache;
using System.Runtime.Serialization;
using System.Runtime.Versioning;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using FluentValidation;
using FluentValidation.Results;
using GongSolutions.Wpf.DragDrop;
using ReactiveUI;
using ReactiveUI.Primitives;
using ReactiveUI.SourceGenerators;
using Velopack.UI.Helpers;
using Velopack.UI.Models;

namespace Velopack.UI;

/// <summary>
/// VelopackModel.
/// </summary>
/// <seealso cref="Velopack.UI.WebConnectionBase" />
/// <seealso cref="GongSolutions.Wpf.DragDrop.IDropTarget" />
[DataContract]
[SupportedOSPlatform("windows10.0.19041.0")]
public partial class VelopackModel : WebConnectionBase, IDropTarget
{
    private readonly ConnectionDiscoveryService connectionDiscoveryService = new();
    private readonly string newFolderName = "NEW FOLDER";
    private List<string>? availableUploadLocation;
    private string? iconFilepath;

    [DataMember]
    [Reactive]
    private string? appId;

    [DataMember]
    [Reactive]
    private string? authors;

    [DataMember]
    [Reactive]
    private string? description;

    [DataMember]
    [Reactive]
    private string? mainExePath;

    [DataMember]
    [Reactive]
    private string? mainExeName;

    [DataMember]
    [Reactive]
    private string? packageFilesOutputPath;

    [DataMember]
    [Reactive]
    private ObservableCollection<ItemLink> packageFiles = [];

    [DataMember]
    [Reactive]
    private WebConnectionBase? selectedConnection;

    [Reactive]
    private ItemLink selectedItem = new();

    [Reactive]
    private SingleFileUpload? selectedUploadItem;

    [DataMember]
    [Reactive]
    private string? velopackOutputPath;

    [DataMember]
    [Reactive]
    private string? title;
    private string? selectedConnectionString;
    private bool setVersionManually;
    private string? splashFilepath;
    private ObservableCollection<SingleFileUpload> uploadQueue = [];
    private string? version;
    private ReactiveCommand<RxVoid, RxVoid>? selectSplashCmd;

    /// <summary>
    /// Velopack options.
    /// </summary>
    [DataMember]
    [Reactive]
    private string? channel = "win";

    [DataMember]
    [Reactive]
    private string? runtime;

    [DataMember]
    [Reactive]
    private string? releaseNotesPath;

    [DataMember]
    [Reactive]
    private string? deltaMode = "BestSpeed";

    [DataMember]
    [Reactive]
    private string? excludeRegex = @".*\.pdb";

    [DataMember]
    [Reactive]
    private bool noPortable;

    [DataMember]
    [Reactive]
    private bool noInstaller;

    [DataMember]
    [Reactive]
    private string? frameworks;

    [DataMember]
    [Reactive]
    private bool skipVeloAppCheck;

    [DataMember]
    [Reactive]
    private string? shortcuts = "Desktop,StartMenuRoot";

    [DataMember]
    [Reactive]
    private bool generateDeltaPackages = true;

    [DataMember]
    [Reactive]
    private bool generateMsi;

    [DataMember]
    [Reactive]
    private string? msiBitness = "x64";

    [DataMember]
    [Reactive]
    private string? signParams;

    [DataMember]
    [Reactive]
    private string? signTemplate;

    [DataMember]
    [Reactive]
    private string? signExclude;

    [DataMember]
    [Reactive]
    private int signParallel = 10;

    [DataMember]
    [Reactive]
    private string? azureTrustedSignFile;

    [DataMember]
    [Reactive]
    private bool useSimplySign;

    [DataMember]
    [Reactive]
    private string? simplySignExecutablePath;

    [DataMember]
    [Reactive]
    private string? simplySignEmail;

    private string? simplySignToken;
    private string? safeSignEnvironment;

    [DataMember]
    [Reactive]
    private string safeSignProvider = "certum";

    [DataMember]
    [Reactive]
    private string? safeSignTimestampUrl = "http://timestamp.digicert.com";

    [DataMember]
    [Reactive]
    private bool msiDeploymentTool;

    [DataMember]
    [Reactive]
    private string? msiDeploymentToolVersion;

    /// <summary>
    /// Persisted base path for FileSystem connection so it can be saved/restored in *.velo.
    /// </summary>
    [DataMember]
    [Reactive]
    private string? fileSystemBasePath;

    /// <summary>
    /// Track subscription to FileSystemConnection.FileSystemPath changes.
    /// </summary>
    private IDisposable? fileSystemConnPathSub;

    /// <summary>
    /// Initializes a new instance of the <see cref="VelopackModel"/> class.
    /// </summary>
    public VelopackModel()
    {
        this.PackageFiles = [];

        // Default to File System connection for ease of use
        this.SelectedConnectionString = "File System";

        // Ensure default connection is resolved immediately
        this.UpdateSelectedConnection(this.SelectedConnectionString);
    }

    /// <summary>
    /// Gets or sets a value indicating whether this instance has unsaved tree changes.
    /// </summary>
    /// <value>
    ///   <c>true</c> if this instance has unsaved tree changes; otherwise, <c>false</c>.
    /// </value>
    [JsonIgnore]
    [ReactiveUI.SourceGenerators.Reactive]
    public partial bool HasUnsavedTreeChanges { get; set; }

    /// <summary>
    /// Gets or sets the current SimplySign authenticator code for this session.
    /// </summary>
    [JsonIgnore]
    [IgnoreDataMember]
    public string? SimplySignToken
    {
        get => this.simplySignToken;
        set => this.RaiseAndSetIfChanged(ref this.simplySignToken, value);
    }

    /// <summary>Gets the available SafeSign provider identifiers.</summary>
    [JsonIgnore]
    [IgnoreDataMember]
    public IReadOnlyList<string> SafeSignProviders => SimplySignSigning.Providers;

    /// <summary>Gets or sets provider environment credentials for this session only.</summary>
    [JsonIgnore]
    [IgnoreDataMember]
    public string? SafeSignEnvironment
    {
        get => this.safeSignEnvironment;
        set => this.RaiseAndSetIfChanged(ref this.safeSignEnvironment, value);
    }

    /// <summary>
    /// Gets the cached connection.
    /// </summary>
    [DataMember]
    [JsonInclude]
    public List<WebConnectionBase> CachedConnection { get; private set; } = [];

    /// <summary>
    /// Gets the available upload location.
    /// </summary>
    /// <value>The available upload location.</value>
    public List<string> AvailableUploadLocation
    {
        get
        {
            this.availableUploadLocation ??= [.. this.connectionDiscoveryService.AvailableConnections.Select(connection => connection.ConnectionName!)];
            return this.availableUploadLocation;
        }
    }

    /// <summary>
    /// Gets the current file path.
    /// </summary>
    /// <value>The current file path.</value>
    [DataMember]
    public string? CurrentFilePath { get; internal set; }

    /// <summary>
    /// Gets or sets the icon filepath.
    /// </summary>
    /// <value>The icon filepath.</value>
    [DataMember]
    public string? IconFilepath
    {
        get => this.iconFilepath;

        set
        {
            this.iconFilepath = value;
            this.RaiseAndSetIfChanged(ref this.iconFilepath, value);
            this.RaisePropertyChanged(nameof(this.IconSource));
        }
    }

    /// <summary>
    /// Gets the icon source.
    /// </summary>
    /// <value>The icon source.</value>
    [JsonIgnore]
    public ImageSource? IconSource
    {
        get
        {
            try
            {
                return GetImageFromFilepath(this.IconFilepath);
            }
            catch
            {
                // TODO -  default icon
                return null;
            }
        }
    }

    /// <summary>
    /// Gets or sets the selected connection string.
    /// </summary>
    /// <value>The selected connection string.</value>
    [DataMember]
    public string? SelectedConnectionString
    {
        get => this.selectedConnectionString;

        set
        {
            if (this.selectedConnectionString == value)
            {
                return;
            }

            this.UpdateSelectedConnection(value);
            this.RaiseAndSetIfChanged(ref this.selectedConnectionString, value);
        }
    }

    /// <summary>
    /// Gets or sets the selected link.
    /// </summary>
    /// <value>The selected link.</value>
    [JsonIgnore]
    [ReactiveUI.SourceGenerators.Reactive]
    public partial IList<ItemLink> SelectedLink { get; set; } = [];

    /// <summary>
    /// Gets the select splash command.
    /// </summary>
    /// <value>The select splash command.</value>
    [JsonIgnore]
    public ICommand SelectSplashCmd => this.selectSplashCmd ??= ReactiveCommand.Create(this.SelectSplash);

    /// <summary>
    /// Gets or sets a value indicating whether [set version manually].
    /// </summary>
    /// <value><c>true</c> if [set version manually]; otherwise, <c>false</c>.</value>
    [DataMember]
    public bool SetVersionManually
    {
        get => this.setVersionManually;

        set
        {
            this.RaiseAndSetIfChanged(ref this.setVersionManually, value);
            this.RefreshPackageVersion();
        }
    }

    /// <summary>
    /// Gets or sets the splash filepath.
    /// </summary>
    /// <value>The splash filepath.</value>
    [DataMember]
    public string? SplashFilepath
    {
        get => this.splashFilepath;

        set
        {
            this.RaiseAndSetIfChanged(ref this.splashFilepath, value);
            this.RaisePropertyChanged(nameof(this.SplashSource));
        }
    }

    /// <summary>
    /// Gets the splash source.
    /// </summary>
    /// <value>The splash source.</value>
    [JsonIgnore]
    public ImageSource? SplashSource
    {
        get
        {
            try
            {
                return GetImageFromFilepath(this.SplashFilepath);
            }
            catch
            {
                return default;
            }
        }
    }

    /// <summary>
    /// Gets or sets the upload queue.
    /// </summary>
    /// <value>The upload queue.</value>
    [JsonIgnore]
    public ObservableCollection<SingleFileUpload> UploadQueue
    {
        get => this.uploadQueue;
        set
        {
            this.uploadQueue = value ?? [];
            this.RaisePropertyChanged(nameof(this.UploadQueue));
        }
    }

    /// <summary>
    /// Gets or sets the version.
    /// </summary>
    /// <value>The version.</value>
    [DataMember]
    public string? Version
    {
        get => this.version;

        set
        {
            if (IsSupportedVelopackVersion(value))
            {
                this.RaiseAndSetIfChanged(ref this.version, value);
            }
            else if (System.Windows.MessageBox.Show("Please use Semantic Versioning 2.0.0 for the version number, for example 1.0.0 or 1.0.0-beta.1. Velopack does not support four-part versions such as 1.0.0.0.", "Invalid Version", MessageBoxButton.OK) == MessageBoxResult.OK)
            {
                this.RefreshPackageVersion();
            }
        }
    }

    /// <summary>
    /// Updates the current drag state.
    /// </summary>
    /// <param name="dropInfo">Information about the drag.</param>
    /// <remarks>
    /// To allow a drop at the current drag position, the <see
    /// cref="P:GongSolutions.Wpf.DragDrop.DropInfo.Effects"/> property on <paramref
    /// name="dropInfo"/> should be set to a value other than <see
    /// cref="F:System.Windows.DragDropEffects.None"/> and <see
    /// cref="P:GongSolutions.Wpf.DragDrop.DropInfo.Data"/> should be set to a non-null value.
    /// </remarks>
    public void DragOver(IDropInfo dropInfo)
    {
        ArgumentNullException.ThrowIfNull(dropInfo);

        dropInfo.DropTargetAdorner = DropTargetAdorners.Highlight;
        dropInfo.Effects = System.Windows.DragDropEffects.Copy;
    }

    /// <summary>
    /// ON DROP.
    /// </summary>
    /// <param name="dropInfo">Object which contains several drop information.</param>
    public void Drop(IDropInfo dropInfo)
    {
        // MOVE FILE INSIDE PACKAGE
        ArgumentNullException.ThrowIfNull(dropInfo);

        var targetItem = dropInfo.TargetItem as ItemLink;

        if (dropInfo.Data is ItemLink draggedItem)
        {
            /* To handle file moving :
             *
             * Step 1 - Remove item from treeview
             * Step 2 - Add as child of target element
             * Step 3 - I update the [OutputFilepath] property,  accordingly to current treeview status.
             *
             */

            dropInfo.DropTargetAdorner = DropTargetAdorners.Highlight;
            dropInfo.Effects = System.Windows.DragDropEffects.Move;

            this.MoveItem(draggedItem, targetItem);
        }

        // FILE ADDED FROM FILE SYSTEM
        if (dropInfo.Data is not DataObject dataObj)
        {
            return;
        }

        if (dataObj.GetDataPresent(System.Windows.DataFormats.FileDrop) && dataObj.GetData(System.Windows.DataFormats.FileDrop) is string[] droppedFiles)
        {
            foreach (var filePath in droppedFiles)
            {
                if (this.ShouldIncludeFile(filePath))
                {
                    this.AddFile(filePath, targetItem);
                }
            }
        }

        this.PackageFiles = OrderFileList(this.PackageFiles);
    }

    /// <summary>
    /// Handles the splash screen selection.
    /// </summary>
    public void SelectSplash()
    {
        var ofd = new System.Windows.Forms.OpenFileDialog
        {
            AddExtension = true,
            DefaultExt = ".gif",
            Filter = "Images|*.gif;*.png;*.jpg;*.jpeg|All Files|*.*",
        };

        var o = ofd.ShowDialog();

        if (o != System.Windows.Forms.DialogResult.OK || !File.Exists(ofd.FileName))
        {
            return;
        }

        this.SplashFilepath = ofd.FileName;
    }

    /// <summary>
    /// Sets the selected item.
    /// </summary>
    /// <param name="item">The item.</param>
    public void SetSelectedItem(IList<ItemLink> item)
    {
        this.SelectedLink = item;
        this.SelectedItem = this.SelectedLink.FirstOrDefault() ?? new ItemLink();
    }

    /// <summary>
    /// Validates this instance.
    /// </summary>
    /// <returns>A <see cref="ValidationResult"/> representing the result of the validation.</returns>
    public override ValidationResult Validate()
    {
        var commonValid = new Validator().Validate(this);
        if (!commonValid.IsValid)
        {
            return commonValid;
        }

        return base.Validate();
    }

    /// <summary>Includes update packages and feeds while excluding standalone installer artifacts.</summary>
    /// <param name="path">The release artifact path.</param>
    /// <returns>True when the artifact should be published for updates.</returns>
    internal static bool IsUpdateArtifact(string path)
    {
        var name = Path.GetFileName(path);
        return !name.Equals("Setup.exe", StringComparison.OrdinalIgnoreCase) &&
            !name.EndsWith("-Setup.exe", StringComparison.OrdinalIgnoreCase) &&
            !name.EndsWith("-Portable.zip", StringComparison.OrdinalIgnoreCase) &&
            !Path.GetExtension(path).Equals(".msi", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Loads an image without retaining a lock on its source file.
    /// </summary>
    /// <param name="path">The image path.</param>
    /// <returns>The loaded image, or null when the file is unavailable.</returns>
    internal static BitmapImage? GetImageFromFilepath(string? path)
    {
        if (!File.Exists(path))
        {
            return null;
        }

        if (File.Exists(path))
        {
            var bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.CacheOption = BitmapCacheOption.None;
            bitmap.UriCachePolicy = new RequestCachePolicy(RequestCacheLevel.BypassCache);
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.CreateOptions = BitmapCreateOptions.IgnoreImageCache;
            bitmap.UriSource = new Uri(path, UriKind.RelativeOrAbsolute);
            bitmap.EndInit();

            return bitmap;
        }

        return new BitmapImage();
    }

    /// <summary>
    /// Sorts each folder with directories first and files ordered by name.
    /// </summary>
    /// <param name="packageFiles">The package tree.</param>
    /// <returns>The ordered package tree.</returns>
    internal static ObservableCollection<ItemLink> OrderFileList(ObservableCollection<ItemLink> packageFiles)
    {
        foreach (var node in packageFiles)
        {
            node.Children = OrderFileList(node.Children);
        }

        return new ObservableCollection<ItemLink>(packageFiles.OrderByDescending(n => n.IsDirectory).ThenBy(n => n.Filename));
    }

    /// <summary>
    /// 29/01/2015
    /// 1) Create update files list
    /// 2) Create queue upload list. Iterating file list foreach connection ( i can have multiple
    /// cloud storage )
    /// 3) Start async upload.
    /// </summary>
    /// <param name="mode">The mode.</param>
    internal void BeginUpdatedFiles(int mode)
    {
        // Determine the folder where vpk outputs artifacts
        var releasesPath = this.VelopackOutputPath;
        if (this.SelectedConnection is FileSystemConnection fsc && !string.IsNullOrWhiteSpace(fsc.FileSystemPath))
        {
            var expected = Path.Combine(fsc.FileSystemPath, PathFolderHelper.ReleasesDirectory);
            if (!string.IsNullOrWhiteSpace(this.VelopackOutputPath) && Directory.Exists(this.VelopackOutputPath))
            {
                releasesPath = this.VelopackOutputPath;
            }
            else
            {
                releasesPath = expected;
            }
        }

        if (string.IsNullOrWhiteSpace(releasesPath) || !Directory.Exists(releasesPath))
        {
            throw new DirectoryNotFoundException("Releases directory " + releasesPath + " not found !");
        }

        if (this.SelectedConnection == null)
        {
            throw new InvalidOperationException("No selected upload location !");
        }

        SingleFileUpload.ResetErrorNotifications();
        this.ApplyPackageContextToConnection(this.SelectedConnection);

        // Gather all artifacts from releases folder (top-level only)
        var allFiles = Directory.EnumerateFiles(releasesPath, "*", SearchOption.TopDirectoryOnly).ToList();

        // Filter for OnlyUpdate mode (mode == 1). mode == 0 => full publish
        if (mode == 1)
        {
            allFiles = [.. allFiles.Where(IsUpdateArtifact)];
        }

        var updatedFiles = allFiles.Where(File.Exists).Select(p => new FileInfo(p)).ToList();

        this.UploadQueue ??= [];
        this.UploadQueue.Clear();

        foreach (var connection in new List<WebConnectionBase>() { this.SelectedConnection })
        {
            foreach (var file in updatedFiles)
            {
                this.UploadQueue.Add(new SingleFileUpload()
                {
                    Filename = Path.GetFileName(file.FullName),
                    ConnectionName = connection.ConnectionName,
                    FileSize = BytesToString(file.Length),
                    Connection = connection,
                    FullPath = file.FullName,
                    ProgressPercentage = 0,
                    UploadStatus = FileUploadStatus.Queued,
                });
            }
        }

        if (this.SelectedConnection is GitHubReleasesConnection { UseGitHubCliAuthentication: true } githubConnection)
        {
            _ = this.PublishGitHubReleaseWithCliAsync(githubConnection, updatedFiles.ConvertAll(file => file.FullName));
            return;
        }

        this.ProcessNextUploadFile();
    }

    /// <summary>
    /// After JSON load, resync selected connection and mirror any saved FileSystemBasePath.
    /// </summary>
    internal void ResyncAfterLoad()
    {
        this.UpdateSelectedConnection(this.SelectedConnectionString);

        if (!this.GenerateDeltaPackages &&
            (string.IsNullOrWhiteSpace(this.DeltaMode) || string.Equals(this.DeltaMode, "BestSpeed", StringComparison.OrdinalIgnoreCase)))
        {
            this.DeltaMode = "None";
        }

        if (string.IsNullOrWhiteSpace(this.DeltaMode))
        {
            this.DeltaMode = "BestSpeed";
        }

        if (string.IsNullOrWhiteSpace(this.ExcludeRegex))
        {
            this.ExcludeRegex = @".*\.pdb";
        }

        if (string.IsNullOrWhiteSpace(this.Shortcuts))
        {
            this.Shortcuts = "Desktop,StartMenuRoot";
        }

        if (!string.IsNullOrWhiteSpace(this.MainExeName) || string.IsNullOrWhiteSpace(this.MainExePath))
        {
            return;
        }

        this.MainExeName = Path.GetFileName(this.MainExePath);
    }

    /// <summary>
    /// Returns the effective delta package mode.
    /// </summary>
    /// <returns>The delta package mode.</returns>
    internal string GetResolvedDeltaMode()
    {
        if (!this.GenerateDeltaPackages && string.IsNullOrWhiteSpace(this.DeltaMode))
        {
            return "None";
        }

        return string.IsNullOrWhiteSpace(this.DeltaMode) ? "BestSpeed" : this.DeltaMode;
    }

    /// <summary>
    /// Clears the package staging directory and recreates it.
    /// </summary>
    internal void ClearPackageFilesDirectory()
    {
        if (string.IsNullOrWhiteSpace(this.PackageFilesOutputPath))
        {
            return;
        }

        Directory.CreateDirectory(this.PackageFilesOutputPath);
        TryDeleteDirectoryContents(this.PackageFilesOutputPath);
        Directory.CreateDirectory(this.PackageFilesOutputPath);
    }

    /// <summary>
    /// Updates the package and release output directories.
    /// </summary>
    /// <param name="basePath">The base output directory.</param>
    internal void SetOutputBasePath(string basePath) => this.ApplyOutputBasePath(basePath);

    /// <summary>
    /// Read the main exe version and set it as package version.
    /// </summary>
    [ReactiveCommand]
    internal void RefreshPackageVersion()
    {
        if (!File.Exists(this.MainExePath))
        {
            return;
        }

        if (this.SetVersionManually)
        {
            return;
        }

        var versInfo = FileVersionInfo.GetVersionInfo(this.MainExePath);

        this.Version = $"{versInfo.ProductMajorPart}.{versInfo.ProductMinorPart}.{versInfo.ProductBuildPart}";
    }

    /// <summary>
    /// Releases unmanaged and - optionally - managed resources.
    /// </summary>
    /// <param name="disposing"><c>true</c> to release both managed and unmanaged resources; <c>false</c> to release only
    /// unmanaged resources.</param>
    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (!disposing)
        {
            return;
        }

        try
        {
            this.selectedItem?.Dispose();
        }
        catch
        {
        }

        if (this.uploadQueue != null)
        {
            foreach (var u in this.uploadQueue)
            {
                try
                {
                    u?.Dispose();
                }
                catch
                {
                }
            }
        }

        this.fileSystemConnPathSub?.Dispose();
        this.fileSystemConnPathSub = null;
    }

    private static string BytesToString(long byteCount)
    {
        string[] suf = ["B", "KB", "MB", "GB", "TB", "PB", "EB"]; // Longs run out around EB
        if (byteCount == 0)
        {
            return "0" + suf[0];
        }

        var bytes = Math.Abs(byteCount);
        var place = Convert.ToInt32(Math.Floor(Math.Log(bytes, 1024)));
        var num = Math.Round(bytes / Math.Pow(1024, place), 1);
        return (Math.Sign(byteCount) * num).ToString() + suf[place];
    }

    private static string GetValidName(string newFolderName, ObservableCollection<ItemLink> children)
    {
        var folderName = newFolderName;

        var ex = children.FirstOrDefault(i => i.Filename == folderName);
        var index = 0;
        while (ex != null)
        {
            index++;
            folderName = newFolderName + " (" + index + ")";

            ex = children.FirstOrDefault(i => i.Filename == folderName);
        }

        return folderName;
    }

    private static void SearchNodeByFilepath(string filepath, ObservableCollection<ItemLink> root, List<ItemLink> rslt)
    {
        foreach (var node in root)
        {
            if (node.SourceFilepath != null && string.Equals(filepath, node.SourceFilepath, StringComparison.CurrentCultureIgnoreCase))
            {
                rslt.Add(node);
            }

            SearchNodeByFilepath(filepath, node.Children, rslt);
        }
    }

    private static bool IsSupportedVelopackVersion(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return true;
        }

        var coreVersion = value.Split(['-', '+'], 2)[0];
        if (coreVersion.Count(c => c == '.') > 2)
        {
            return false;
        }

        return SemanticVersion.TryParse(value, out _);
    }

    private static void TryDeleteFile(string? path)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            {
                return;
            }

            File.SetAttributes(path, FileAttributes.Normal);
            File.Delete(path);
        }
        catch
        {
            // ignore
        }
    }

    private static void TryDeleteDirectory(string? path)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(path) || !Directory.Exists(path))
            {
                return;
            }

            // ensure files are deletable
            foreach (var f in Directory.EnumerateFiles(path!, "*", SearchOption.AllDirectories))
            {
                try
                {
                    File.SetAttributes(f, FileAttributes.Normal);
                }
                catch
                {
                }
            }

            Directory.Delete(path!, true);
        }
        catch
        {
            // ignore
        }
    }

    private static void TryDeleteDirectoryContents(string? path)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(path) || !Directory.Exists(path))
            {
                return;
            }

            foreach (var file in Directory.EnumerateFiles(path!, "*", SearchOption.TopDirectoryOnly))
            {
                TryDeleteFile(file);
            }

            foreach (var dir in Directory.EnumerateDirectories(path!, "*", SearchOption.TopDirectoryOnly))
            {
                TryDeleteDirectory(dir);
            }
        }
        catch
        {
            // ignore
        }
    }

    private static void OpenFolder(string path)
    {
        Directory.CreateDirectory(path);
        Process.Start(new ProcessStartInfo
        {
            FileName = path,
            UseShellExecute = true,
        });
    }

    /// <summary>
    /// -------- Helpers added for filtering and upload processing --------.
    /// </summary>
    private bool ShouldIncludeFile(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return false;
        }

        try
        {
            var attr = File.GetAttributes(path);
            if (attr.HasFlag(FileAttributes.Directory))
            {
                return true; // always include directories
            }

            if (!string.IsNullOrWhiteSpace(this.ExcludeRegex) &&
                Regex.IsMatch(path, this.ExcludeRegex, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant))
            {
                return false;
            }

            return File.Exists(path);
        }
        catch
        {
            return false;
        }
    }

    private void ApplyOutputBasePath(string? basePath)
    {
        if (string.IsNullOrWhiteSpace(basePath))
        {
            return;
        }

        this.FileSystemBasePath = basePath;
        this.PackageFilesOutputPath = Path.Combine(basePath, PathFolderHelper.PackageFilesDirectory);
        this.VelopackOutputPath = Path.Combine(basePath, PathFolderHelper.ReleasesDirectory);
    }

    private void ApplyPackageContextToConnection(WebConnectionBase? connection)
    {
        if (connection is not GitHubReleasesConnection github)
        {
            return;
        }

        github.PackageTitle = this.Title;
        github.PackageChannel = this.Channel;
    }

    /// <summary>
    /// Opens the connection edit dialog for the currently selected connection.
    /// </summary>
    [ReactiveCommand]
    private void EditCurrentConnection()
    {
        // Ensure we have a connection instance matching the selected string
        if (string.IsNullOrWhiteSpace(this.SelectedConnectionString))
        {
            return;
        }

        var conn = this.SelectedConnection ?? this.connectionDiscoveryService.GetByName(this.SelectedConnectionString);
        if (conn == null)
        {
            return;
        }

        // Cache the instance so changes persist across selection switches
        this.CachedConnection ??= [];
        if (!this.CachedConnection.Contains(conn))
        {
            this.CachedConnection.Add(conn);
        }

        this.ApplyPackageContextToConnection(conn);

        var dlg = new WebConnectionEdit
        {
            DataContext = conn,
            Owner = Application.Current?.Windows.OfType<Window>().FirstOrDefault(w => w.IsActive) ?? Application.Current?.MainWindow,
        };

        var ok = dlg.ShowDialog();
        if (ok != true)
        {
            return;
        }

        // Keep SelectedConnection in sync
        this.SelectedConnection = conn;

        // If editing a FileSystem connection, mirror path into model for visibility/persistence
        if (conn is not FileSystemConnection fsc || string.IsNullOrWhiteSpace(fsc.FileSystemPath))
        {
            return;
        }

        this.ApplyOutputBasePath(fsc.FileSystemPath);
    }

    /// <summary>
    /// Choose files to add to package (multi-select).
    /// </summary>
    [ReactiveCommand]
    private void AddFilesFromDialog()
    {
        var ofd = new Microsoft.Win32.OpenFileDialog
        {
            Multiselect = true,
            CheckFileExists = true,
            Title = "Select files to include",
        };
        if (ofd.ShowDialog() != true)
        {
            return;
        }

        foreach (var f in ofd.FileNames)
        {
            if (this.ShouldIncludeFile(f))
            {
                this.AddFile(f, null);
            }
        }

        this.PackageFiles = OrderFileList(this.PackageFiles);
    }

    /// <summary>
    /// Choose a folder (e.g. bin/Release) to include recursively.
    /// </summary>
    [ReactiveCommand]
    private void AddFolderFromDialog()
    {
        using var dialog = new System.Windows.Forms.FolderBrowserDialog();
        var result = dialog.ShowDialog();
        if (result != System.Windows.Forms.DialogResult.OK || !Directory.Exists(dialog.SelectedPath))
        {
            return;
        }

        this.AddFile(dialog.SelectedPath, null);
        this.PackageFiles = OrderFileList(this.PackageFiles);
    }

    /// <summary>
    /// Selects the nupkg directory.
    /// </summary>
    [ReactiveCommand]
    private void SelectNupkgDirectory()
    {
        var dialog = new System.Windows.Forms.FolderBrowserDialog();

        if (Directory.Exists(this.PackageFilesOutputPath))
        {
            dialog.SelectedPath = this.PackageFilesOutputPath;
        }

        var result = dialog.ShowDialog();

        if (result != System.Windows.Forms.DialogResult.OK)
        {
            return;
        }

        this.PackageFilesOutputPath = dialog.SelectedPath;
    }

    /// <summary>
    /// Selects the output directory.
    /// </summary>
    [ReactiveCommand]
    private void SelectOutputDirectory()
    {
        var dialog = new System.Windows.Forms.FolderBrowserDialog();

        if (Directory.Exists(this.VelopackOutputPath))
        {
            dialog.SelectedPath = this.VelopackOutputPath;
        }

        var result = dialog.ShowDialog();

        if (result != System.Windows.Forms.DialogResult.OK)
        {
            return;
        }

        this.VelopackOutputPath = dialog.SelectedPath;

        // If FileSystem connection is selected, align its path and use it for uploads
        if (this.SelectedConnection is not FileSystemConnection fsc)
        {
            return;
        }

        fsc.FileSystemPath = Path.GetDirectoryName(this.VelopackOutputPath) ?? this.VelopackOutputPath;
        this.ApplyOutputBasePath(fsc.FileSystemPath);
    }

    /// <summary>
    /// Opens the staged package files folder.
    /// </summary>
    [ReactiveCommand]
    private void OpenPackageFiles()
    {
        if (string.IsNullOrWhiteSpace(this.PackageFilesOutputPath))
        {
            return;
        }

        OpenFolder(this.PackageFilesOutputPath);
    }

    /// <summary>
    /// Opens the Velopack releases folder.
    /// </summary>
    [ReactiveCommand]
    private void OpenReleases()
    {
        if (string.IsNullOrWhiteSpace(this.VelopackOutputPath))
        {
            return;
        }

        OpenFolder(this.VelopackOutputPath);
    }

    /// <summary>
    /// Clears the staged package files folder without changing the source file tree.
    /// </summary>
    [ReactiveCommand]
    private void ClearPackageFiles()
    {
        this.ClearPackageFilesDirectory();
    }

    /// <summary>
    /// Choose an application icon file and update IconFilepath.
    /// </summary>
    [ReactiveCommand]
    private void SelectIcon()
    {
        var ofd = new System.Windows.Forms.OpenFileDialog
        {
            AddExtension = true,
            DefaultExt = ".ico",
            Filter = "Icon or Image|*.ico;*.png;*.jpg;*.jpeg;*.bmp|All Files|*.*",
            Title = "Select application icon",
        };

        var result = ofd.ShowDialog();
        if (result != System.Windows.Forms.DialogResult.OK || !File.Exists(ofd.FileName))
        {
            return;
        }

        this.IconFilepath = ofd.FileName;
    }

    /// <summary>
    /// Choose release notes for this Velopack release.
    /// </summary>
    [ReactiveCommand]
    private void SelectReleaseNotes()
    {
        var ofd = new System.Windows.Forms.OpenFileDialog
        {
            AddExtension = true,
            DefaultExt = ".md",
            Filter = "Release notes|*.md;*.txt|All Files|*.*",
            Title = "Select release notes",
        };

        var result = ofd.ShowDialog();
        if (result != System.Windows.Forms.DialogResult.OK || !File.Exists(ofd.FileName))
        {
            return;
        }

        this.ReleaseNotesPath = ofd.FileName;
    }

    /// <summary>
    /// Choose Azure Trusted Signing metadata for Velopack signing.
    /// </summary>
    [ReactiveCommand]
    private void SelectAzureTrustedSignFile()
    {
        var ofd = new System.Windows.Forms.OpenFileDialog
        {
            AddExtension = true,
            DefaultExt = ".json",
            Filter = "JSON|*.json|All Files|*.*",
            Title = "Select Azure Trusted Signing metadata",
        };

        var result = ofd.ShowDialog();
        if (result != System.Windows.Forms.DialogResult.OK || !File.Exists(ofd.FileName))
        {
            return;
        }

        this.AzureTrustedSignFile = ofd.FileName;
    }

    /// <summary>
    /// Selects the installed SafeSign command line executable.
    /// </summary>
    [ReactiveCommand]
    private void SelectSimplySignExecutable()
    {
        using var dialog = new System.Windows.Forms.OpenFileDialog
        {
            Filter = "SafeSign executable|safesign.exe|Executable files|*.exe",
            Title = "Select installed safesign.exe",
            CheckFileExists = true,
        };
        if (dialog.ShowDialog() != System.Windows.Forms.DialogResult.OK)
        {
            return;
        }

        this.SimplySignExecutablePath = dialog.FileName;
    }

    private void AddFile(string filePath, ItemLink? targetItem)
    {
        var isDir = false;
        var fa = File.GetAttributes(filePath);
        if (fa.HasFlag(FileAttributes.Directory))
        {
            isDir = true;
        }

        this.RemoveItemBySourceFilepath(filePath);

        var node = new ItemLink() { SourceFilepath = filePath, IsDirectory = isDir };

        var parent = targetItem;
        if (targetItem == null)
        {
            // Add to root
            this.packageFiles.Add(node);
        }
        else
        {
            if (!targetItem.IsDirectory)
            {
                parent = targetItem.GetParent(this.PackageFiles);
            }

            if (parent != null)
            {
                // Insert into treeview root
                parent.Children.Add(node);
            }
            else
            {
                // Insert into treeview root
                this.packageFiles.Add(node);
            }
        }

        this.HasUnsavedTreeChanges = true;

        if (isDir)
        {
            var dir = new DirectoryInfo(filePath);

            var files = dir.GetFiles("*.*", SearchOption.TopDirectoryOnly);
            var subDirectory = dir.GetDirectories("*.*", SearchOption.TopDirectoryOnly);

            foreach (var f in files)
            {
                if (this.ShouldIncludeFile(f.FullName))
                {
                    this.AddFile(f.FullName, node);
                }
            }

            foreach (var f in subDirectory)
            {
                this.AddFile(f.FullName, node);
            }
        }
        else
        {
            // I keep the exe filepath, i'll read the version from this file.
            var ext = Path.GetExtension(filePath).ToLower();

            if (ext == ".exe")
            {
                var nodeParent = node.GetParent(this.PackageFiles);
                if (nodeParent == null)
                {
                    var fileName = Path.GetFileNameWithoutExtension(filePath);
                    if (string.IsNullOrWhiteSpace(this.AppId))
                    {
                        this.AppId = fileName;
                    }

                    if (string.IsNullOrWhiteSpace(this.Title))
                    {
                        this.Title = fileName;
                    }

                    this.MainExePath = filePath;
                    this.MainExeName = Path.GetFileName(filePath);
                    var versInfo = FileVersionInfo.GetVersionInfo(this.MainExePath);
                    if (string.IsNullOrWhiteSpace(this.Description))
                    {
                        this.Description = versInfo.Comments;
                    }

                    if (string.IsNullOrWhiteSpace(this.Authors))
                    {
                        this.Authors = versInfo.CompanyName;
                    }

                    this.RefreshPackageVersion();
                }
            }
        }
    }

    [ReactiveCommand]
    private void AddDirectory()
    {
        if (this.SelectedLink.Count != 1)
        {
            return;
        }

        var selectedLink = this.SelectedLink[0];
        if (selectedLink != null)
        {
            var validFolderName = GetValidName(this.newFolderName, selectedLink.Children);

            selectedLink.Children.Add(new ItemLink { OutputFilename = validFolderName, IsDirectory = true });
        }
        else
        {
            var validFolderName = GetValidName(this.newFolderName, this.PackageFiles);

            this.PackageFiles.Add(new ItemLink { OutputFilename = validFolderName, IsDirectory = true });
        }

        this.HasUnsavedTreeChanges = true;
    }

    /// <summary>
    /// Remove selected item(s) from the tree.
    /// </summary>
    [ReactiveCommand]
    private void RemoveItem()
    {
        if (this.SelectedLink == null || this.SelectedLink.Count == 0)
        {
            return;
        }

        foreach (var item in this.SelectedLink.ToList())
        {
            this.RemoveFromTreeview(item);
        }

        this.PackageFiles = OrderFileList(this.PackageFiles);
        this.HasUnsavedTreeChanges = true;
    }

    /// <summary>
    /// Remove all items (if one selected, clear its parent children, otherwise clear root).
    /// </summary>
    [ReactiveCommand]
    private void RemoveAllItems()
    {
        if (this.SelectedLink?.Count == 1)
        {
            this.RemoveAllFromTreeview(this.SelectedLink[0]);
        }
        else
        {
            // Also clean PackageFiles folder on disk
            TryDeleteDirectoryContents(this.PackageFilesOutputPath);

            this.packageFiles.Clear();
            this.RaisePropertyChanged(nameof(this.PackageFiles));
            this.MainExePath = string.Empty;
            this.RefreshPackageVersion();
            this.HasUnsavedTreeChanges = true;
        }
    }

    /// <summary>
    /// Moves the item.
    /// </summary>
    /// <param name="draggedItem">The dragged item.</param>
    /// <param name="targetItem">The target item.</param>
    private void MoveItem(ItemLink draggedItem, ItemLink? targetItem)
    {
        // Remove from current location
        this.RemoveFromTreeview(draggedItem);

        // Add to target position
        var parent = targetItem;
        if (targetItem == null)
        {
            // Porto su root
            this.packageFiles.Add(draggedItem);
        }
        else
        {
            if (!targetItem.IsDirectory)
            {
                parent = targetItem.GetParent(this.PackageFiles);
            }

            if (parent != null)
            {
                // Insert into treeview root
                parent.Children.Add(draggedItem);
            }
            else
            {
                // Insert into treeview root
                this.packageFiles.Add(draggedItem);
            }
        }

        this.HasUnsavedTreeChanges = true;
        this.RaisePropertyChanged(nameof(this.PackageFiles));
    }

    private void RemoveAllFromTreeview(ItemLink item)
    {
        var parent = item.GetParent(this.PackageFiles);

        // Element is in the treeview root.
        if (parent == null)
        {
            // Clear disk mirror under PackageFiles root
            TryDeleteDirectoryContents(this.PackageFilesOutputPath);

            this.packageFiles.Clear();
            this.RaisePropertyChanged(nameof(this.PackageFiles));
        }
        else
        {
            // Remove all children from disk under this parent's directory
            var parentPath = this.BuildDirectoryFullPath(parent);
            TryDeleteDirectoryContents(parentPath);

            // Remove it from children list
            parent.Children.Clear();
        }

        this.MainExePath = string.Empty;
        this.RefreshPackageVersion();
        this.HasUnsavedTreeChanges = true;
    }

    private void RemoveFromTreeview(ItemLink item)
    {
        var parent = item.GetParent(this.PackageFiles);

        if (this.MainExePath != null && item.SourceFilepath != null && string.Equals(this.MainExePath, item.SourceFilepath, StringComparison.CurrentCultureIgnoreCase))
        {
            this.MainExePath = string.Empty;
            this.RefreshPackageVersion();
        }

        // Delete corresponding file/folder from disk mirror
        this.TryDeleteNodeFromDisk(item);

        // Element is in the treeview root.
        if (parent == null)
        {
            this.packageFiles.Remove(item);
        }
        else
        {
            // Remove it from children list
            parent.Children.Remove(item);
        }

        this.HasUnsavedTreeChanges = true;
    }

    private void RemoveItemBySourceFilepath(string filepath)
    {
        var list = new List<ItemLink>();

        SearchNodeByFilepath(filepath, this.PackageFiles, list);

        foreach (var node in list)
        {
            this.RemoveFromTreeview(node);
        }
    }

    /// <summary>
    /// I keep in memory created WebConnectionBase, so if the user switch accidentally the
    /// connection string , he don't lose inserted parameter.
    /// </summary>
    /// <param name="connectionType">Type of the connection.</param>
    private void UpdateSelectedConnection(string? connectionType)
    {
        if (string.IsNullOrWhiteSpace(connectionType))
        {
            return;
        }

        // Instantiate cache if null
        this.CachedConnection ??= [];

        // Retrieve cached connection or take new instance from connection service
        var con =
            this.CachedConnection.FirstOrDefault(c => c.ConnectionName == connectionType) ??
            this.connectionDiscoveryService.GetByName(connectionType);

        // Cache connection if not cached already
        if (con != null && !this.CachedConnection.Contains(con))
        {
            this.CachedConnection.Add(con);
        }

        this.SelectedConnection = con;
        this.ApplyPackageContextToConnection(this.SelectedConnection);

        // Remove previous subscription
        this.fileSystemConnPathSub?.Dispose();
        this.fileSystemConnPathSub = null;

        // If FileSystem connection becomes active, sync paths both ways for persistence and UI visibility
        if (this.SelectedConnection is not FileSystemConnection fsc)
        {
            return;
        }

        if (!string.IsNullOrWhiteSpace(this.FileSystemBasePath))
        {
            // Prefer restored model value when available
            fsc.FileSystemPath = this.FileSystemBasePath;
        }
        else if (!string.IsNullOrWhiteSpace(fsc.FileSystemPath))
        {
            // If connection already has a path, mirror to model
            this.FileSystemBasePath = fsc.FileSystemPath;
        }

        this.ApplyOutputBasePath(fsc.FileSystemPath);

        // Observe changes to the connection path and propagate into model for persistence/dirty tracking
        this.fileSystemConnPathSub = fsc
            .WhenAnyValue(x => x.FileSystemPath)
            .Subscribe(path => this.ApplyOutputBasePath(path));
    }

    private async Task PublishGitHubReleaseWithCliAsync(GitHubReleasesConnection connection, IReadOnlyCollection<string> files)
    {
        try
        {
            foreach (var item in this.UploadQueue.Where(item => item.UploadStatus == FileUploadStatus.Queued))
            {
                item.UploadStatus = FileUploadStatus.InProgress;
                item.ProgressPercentage = 5;
            }

            await GitHubCliReleasePublisher.PublishAsync(connection, files).ConfigureAwait(true);

            foreach (var item in this.UploadQueue)
            {
                item.UploadStatus = FileUploadStatus.Completed;
                item.ProgressPercentage = 100;
            }
        }
        catch (Exception ex)
        {
            foreach (var item in this.UploadQueue.Where(item => item.UploadStatus != FileUploadStatus.Completed))
            {
                item.UploadStatus = FileUploadStatus.Failed;
            }

            MessageBox.Show(ex.Message, "GitHub Release Publish Failed", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private async void ProcessNextUploadFile()
    {
        if (this.UploadQueue == null || this.UploadQueue.Count == 0)
        {
            return;
        }

        foreach (var item in this.UploadQueue.Where(f => f.UploadStatus == FileUploadStatus.Queued).ToList())
        {
            item.UploadStatus = FileUploadStatus.InProgress;
            try
            {
                // For FileSystem connection, ensure the file exists at the destination
                if (item.Connection is FileSystemConnection fsConn && !string.IsNullOrWhiteSpace(fsConn.FileSystemPath))
                {
                    item.ProgressPercentage = 100;
                    item.UploadStatus = FileUploadStatus.Completed;
                }
                else
                {
                    await item.StartUploadAsync().ConfigureAwait(true);
                }
            }
            catch (Exception ex)
            {
                item.ProgressPercentage = 100;
                item.UploadStatus = FileUploadStatus.Failed;
                MessageBox.Show(ex.Message, "Upload Failed", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }

    /// <summary>
    /// -------- Disk sync helpers for PackageFiles mirror --------.
    /// </summary>
    private string? BuildDirectoryFullPath(ItemLink? node)
    {
        if (node == null || string.IsNullOrWhiteSpace(this.PackageFilesOutputPath))
        {
            return null;
        }

        var parts = new List<string>();

        // collect ancestors up to root
        var current = node;
        while (true)
        {
            var parent = current.GetParent(this.PackageFiles);
            if (parent == null)
            {
                // current is at root level
                parts.Add(current.Filename);
                break;
            }

            parts.Add(current.Filename);
            current = parent;
        }

        parts.Reverse();
        return Path.Combine(this.PackageFilesOutputPath!, Path.Combine([.. parts]));
    }

    private string? BuildParentsDirectoryFullPath(ItemLink? node)
    {
        if (node == null || string.IsNullOrWhiteSpace(this.PackageFilesOutputPath))
        {
            return null;
        }

        var stack = new Stack<string>();
        var current = node.GetParent(this.PackageFiles);
        while (current != null)
        {
            stack.Push(current.Filename);
            current = current.GetParent(this.PackageFiles);
        }

        return stack.Count == 0
            ? this.PackageFilesOutputPath
            : Path.Combine(this.PackageFilesOutputPath!, Path.Combine([.. stack]));
    }

    private void TryDeleteNodeFromDisk(ItemLink item)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(this.PackageFilesOutputPath))
            {
                return;
            }

            if (item.IsDirectory)
            {
                var dir = this.BuildDirectoryFullPath(item);
                TryDeleteDirectory(dir);
            }
            else
            {
                var parentDir = this.BuildParentsDirectoryFullPath(item);
                if (string.IsNullOrWhiteSpace(parentDir))
                {
                    return;
                }

                var filePath = Path.Combine(parentDir!, item.Filename);
                TryDeleteFile(filePath);
            }
        }
        catch
        {
            // ignore
        }
    }

    private class Validator : AbstractValidator<VelopackModel>
    {
        public Validator()
        {
            this.RuleFor(c => c.AppId).NotEmpty();
            this.RuleFor(c => c.Title).NotEmpty();
            this.RuleFor(c => c.Description).NotEmpty();
            this.RuleFor(c => c.Version).NotEmpty();
            this.RuleFor(c => c.PackageFiles).NotEmpty();
            this.RuleFor(c => c.Authors).NotEmpty();
            this.RuleFor(c => c.SelectedConnectionString).NotEmpty();
            this.RuleFor(c => c.Version).Must(IsSupportedVelopackVersion).WithMessage("Version must be semver2 and cannot be a four-part version.");
            this.RuleFor(c => c.DeltaMode).Must(mode => string.IsNullOrWhiteSpace(mode) || new[] { "BestSpeed", "BestSize", "None" }.Contains(mode)).WithMessage("Delta mode must be BestSpeed, BestSize, or None.");
            this.RuleFor(c => c.ExcludeRegex).Must(regex =>
            {
                if (string.IsNullOrWhiteSpace(regex))
                {
                    return true;
                }

                try
                {
                    _ = new Regex(regex);
                    return true;
                }
                catch (ArgumentException)
                {
                    return false;
                }
            }).WithMessage("Exclude must be a valid regular expression.");
            this.RuleFor(c => c.SignParallel).GreaterThan(0);
            this.RuleFor(c => c).Custom((model, context) =>
            {
                if (!model.UseSimplySign || !(SimplySignSigning.GetValidationError(model) is { } error))
                {
                    return;
                }

                context.AddFailure(nameof(VelopackModel.UseSimplySign), error);
            });
        }
    }
}
