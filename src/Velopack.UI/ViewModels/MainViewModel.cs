// Copyright (c) Chris Pulman. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Reactive;
using System.Reactive.Linq;
using System.Runtime.Versioning;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using System.Windows;
using CrissCross;
using Microsoft.Win32;
using ReactiveUI;
using ReactiveUI.SourceGenerators;
using Velopack.UI.Helpers;

namespace Velopack.UI;

/// <summary>
/// MainViewModel.
/// </summary>
/// <seealso cref="CrissCross.RxObject" />
[SupportedOSPlatform("windows10.0.19041.0")]
public partial class MainViewModel : RxObject
{
    private readonly JsonSerializerOptions _saveOptions;
    private BackgroundWorker? _activeBackgroungWorker;
    private bool _abortPackageFlag;
    [Reactive]
    private string? _currentPackageCreationStage;
    [Reactive]
    private bool _isBusy;
    [Reactive]
    private VelopackModel? _model;
    private bool _isSaved;
    private int _publishMode;
    private Process? _exeProcess;
    private string? _filePath;
    private IDisposable? _dirtySubscription;
    private bool _hasUnsavedNonTreeChanges;

    /// <summary>
    /// Initializes a new instance of the <see cref="MainViewModel"/> class.
    /// </summary>
    public MainViewModel()
    {
        _saveOptions = new JsonSerializerOptions
        {
            WriteIndented = false,
            TypeInfoResolver = new DefaultJsonTypeInfoResolver
            {
                Modifiers =
                {
                    ti =>
                    {
                        if (ti.Type == typeof(VelopackModel))
                        {
                            var namesToRemove = new HashSet<string>
                            {
                                nameof(VelopackModel.IconSource),
                                nameof(VelopackModel.SplashSource),
                                nameof(VelopackModel.SelectSplashCmd),
                                nameof(VelopackModel.SelectedLink),
                                nameof(VelopackModel.UploadQueue),
                                nameof(VelopackModel.SelectedConnection),
                                nameof(VelopackModel.SelectedUploadItem),
                                nameof(VelopackModel.HasUnsavedTreeChanges)
                            };
                            foreach (var prop in ti.Properties.ToList())
                            {
                                if (namesToRemove.Contains(prop.Name))
                                {
                                    ti.Properties.Remove(prop);
                                }
                            }
                        }
                        else if (ti.Type == typeof(ItemLink))
                        {
                            var namesToRemove = new HashSet<string>
                            {
                                nameof(ItemLink.FileIcon),
                                nameof(ItemLink.HasDummyChild)
                            };
                            foreach (var prop in ti.Properties.ToList())
                            {
                                if (namesToRemove.Contains(prop.Name))
                                {
                                    ti.Properties.Remove(prop);
                                }
                            }
                        }
                        else if (ti.Type == typeof(WebConnectionBase))
                        {
                            ti.PolymorphismOptions = new JsonPolymorphismOptions
                            {
                                TypeDiscriminatorPropertyName = "$type",
                                DerivedTypes =
                                {
                                    new JsonDerivedType(typeof(GitHubReleasesConnection), "GitHubReleasesConnection"),
                                    new JsonDerivedType(typeof(AmazonS3Connection), "AmazonS3Connection"),
                                    new JsonDerivedType(typeof(FileSystemConnection), "FileSystemConnection"),
                                }
                            };
                        }
                    }
                }
            }
        };

        Model = new VelopackModel();
        SetupDirtyTracking();

        UserPreference = PathFolderHelper.LoadUserPreference();

        var startupProject = App.StartupProjectFilePath;
        var last = string.IsNullOrWhiteSpace(startupProject)
            ? UserPreference.LastOpenedProject.LastOrDefault()
            : startupProject;

        if (!string.IsNullOrEmpty(last) && File.Exists(last))
        {
            OpenProject(last);
        }

        AbortPackageCreationCmd = ReactiveCommand.Create(AbortPackageCreation);
    }

    /// <summary>
    /// Gets a value indicating whether this instance has unsaved changes.
    /// </summary>
    /// <value>
    ///   <c>true</c> if this instance has unsaved changes; otherwise, <c>false</c>.
    /// </value>
    public bool HasUnsavedChanges => (Model?.HasUnsavedTreeChanges ?? false) || _hasUnsavedNonTreeChanges || string.IsNullOrWhiteSpace(FilePath);

    /// <summary>
    /// Gets the abort package creation command.
    /// </summary>
    /// <value>The abort package creation command.</value>
    public ReactiveCommand<Unit, Unit> AbortPackageCreationCmd { get; }

    /// <summary>
    /// Gets or sets the file path.
    /// </summary>
    /// <value>The file path.</value>
    public string? FilePath
    {
        get => _filePath;

        set
        {
            Model?.CurrentFilePath = value;

            this.RaiseAndSetIfChanged(ref _filePath, value);
        }
    }

    /// <summary>
    /// Gets the user preference.
    /// </summary>
    public Preference UserPreference { get; }

    /// <summary>
    /// Gets the window title.
    /// </summary>
    /// <value>The window title.</value>
    public string WindowTitle
    {
        get
        {
            var fp = "New Project*";
            if (!string.IsNullOrWhiteSpace(FilePath))
            {
                fp = Path.GetFileNameWithoutExtension(FilePath);
            }

            return $"{PathFolderHelper.ProgramName} {PathFolderHelper.GetProgramVersion()} - {fp}";
        }
    }

    /// <summary>
    /// Aborts the package creation.
    /// </summary>
    public void AbortPackageCreation()
    {
        if (_activeBackgroungWorker != null)
        {
            _activeBackgroungWorker.CancelAsync();

            _exeProcess?.Kill();
        }

        _abortPackageFlag = true;
    }

    /// <summary>
    /// Creates the new project.
    /// </summary>
    [ReactiveCommand]
    public void CreateNewProject()
    {
        var rslt = MessageBox.Show("Save current project?", "New Project", MessageBoxButton.YesNoCancel, MessageBoxImage.Question);

        if (rslt == MessageBoxResult.Cancel)
        {
            return;
        }

        if (rslt == MessageBoxResult.Yes)
        {
            Save();
        }

        Model = new VelopackModel();
        SetupDirtyTracking();
        _hasUnsavedNonTreeChanges = false;
        Model.HasUnsavedTreeChanges = false;
        this.RaisePropertyChanged(nameof(HasUnsavedChanges));
    }

    /// <summary>
    /// Opens the project.
    /// </summary>
    [ReactiveCommand]
    public void OpenProject()
    {
        try
        {
            var ofd = new OpenFileDialog
            {
                AddExtension = true,
                DefaultExt = PathFolderHelper.ProjectFileExtension,
                Filter = PathFolderHelper.FileDialogName,
                Multiselect = false
            };

            var iniDir = PathFolderHelper.GetMyDirectory(MyDirectory.Project);
            if (!string.IsNullOrWhiteSpace(iniDir))
            {
                ofd.InitialDirectory = iniDir;
            }

            if (ofd.ShowDialog() != true || !File.Exists(ofd.FileName))
            {
                return;
            }

            OpenProject(ofd.FileName);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Loading File Error: {ex}", "Error", MessageBoxButton.OK, MessageBoxImage.Error, MessageBoxResult.None);
        }
    }

    /// <summary>
    /// Opens the project.
    /// </summary>
    /// <param name="filepath">The filepath.</param>
    public void OpenProject(string filepath)
    {
        try
        {
            if (string.IsNullOrEmpty(filepath) || !File.Exists(filepath))
            {
                MessageBox.Show("This file doesn't exist : " + filepath, "Error", MessageBoxButton.OK, MessageBoxImage.Error, MessageBoxResult.None);
                return;
            }

            FilePath = filepath;

            var m = JsonSerializer.Deserialize<VelopackModel>(File.ReadAllText(filepath), _saveOptions);

            if (m == null)
            {
                return;
            }

            Model = m;

            // Resync connection instances and mirror saved FileSystemBasePath into the active FileSystemConnection
            Model.ResyncAfterLoad();
            if (Model.SelectedConnection is not FileSystemConnection)
            {
                var projectDir = Path.GetDirectoryName(filepath);
                if (!string.IsNullOrWhiteSpace(projectDir))
                {
                    Model.SetOutputBasePath(projectDir);
                }
            }

            SetupDirtyTracking();
            Model.PackageFiles = VelopackModel.OrderFileList(Model.PackageFiles);
            Model.RefreshPackageVersion();
            AddLastProject(filepath);
            this.RaisePropertyChanged(nameof(WindowTitle));

            // opened from disk -> clean dirty flags
            _hasUnsavedNonTreeChanges = false;
            Model.HasUnsavedTreeChanges = false;
            this.RaisePropertyChanged(nameof(HasUnsavedChanges));
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Loading File Error: {ex}", "Error", MessageBoxButton.OK, MessageBoxImage.Error, MessageBoxResult.None);
        }
    }

    /// <summary>
    /// Publishes the package.
    /// </summary>
    /// <exception cref="Exception">
    /// Package Details are invalid or incomplete ! or Selected connection details are not valid !.
    /// </exception>
    public void PublishPackage()
    {
        try
        {
            if (_activeBackgroungWorker?.IsBusy == true)
            {
                Trace.TraceError("You shouldn't be here !");
                return;
            }

            Model?.UploadQueue.Clear();
            Model?.RefreshPackageVersion();

            Trace.WriteLine("START PUBLISHING ! : " + Model?.Title);

            // 1) Check validity
            if (Model?.IsValid == false)
            {
                throw new Exception("Package Details are invalid or incomplete !");
            }

            if (Model?.SelectedConnection == null || !Model.SelectedConnection.IsValid)
            {
                throw new Exception("Selected connection details are not valid !");
            }

            Trace.WriteLine("DATA VALIDATE - OK ! ");

            Save();

            // I proceed only if i created the project .velo file and directory I need existing
            // directory to create the packages.
            if (!_isSaved)
            {
                return;
            }

            IsBusy = true;

            _activeBackgroungWorker = new BackgroundWorker() { WorkerReportsProgress = true, WorkerSupportsCancellation = true };

            _activeBackgroungWorker.DoWork += ActiveBackgroungWorker_DoWork;
            _activeBackgroungWorker.RunWorkerCompleted += PackageCreationCompleted;
            _activeBackgroungWorker.ProgressChanged += ActiveBackgroungWorker_ProgressChanged;

            _activeBackgroungWorker.RunWorkerAsync(this);
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.ToString(), "Error on publishing", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    /// <summary>
    /// 1) Check field validity
    /// 2) Create Nuget package
    /// 3) Squirrel relasify
    /// 4) Publish to amazon the updated file ( to get the update file , search the timedate &gt;
    ///    of building time ) /// - Possibly in async way..
    /// - Must be callable from command line, so i can optionally start this process from at the
    ///   end of visual studio release build.
    /// </summary>
    [ReactiveCommand]
    public void PublishPackageComplete()
    {
        _publishMode = 0;
        PublishPackage();
    }

    /// <summary>
    /// Publishes the package only update.
    /// </summary>
    [ReactiveCommand]
    public void PublishPackageOnlyUpdate()
    {
        _publishMode = 1;
        PublishPackage();
    }

    /// <summary>
    /// Saves this instance.
    /// </summary>
    [ReactiveCommand]
    public void Save()
    {
        if (Model == null)
        {
            return;
        }

        var fileSystemPath = Model.SelectedConnection is FileSystemConnection fsc && !string.IsNullOrWhiteSpace(fsc.FileSystemPath)
            ? fsc.FileSystemPath
            : null;

        if (string.IsNullOrWhiteSpace(FilePath))
        {
            if (!string.IsNullOrWhiteSpace(fileSystemPath))
            {
                FilePath = fileSystemPath;
            }
            else
            {
                SaveAs();
                return;
            }
        }

        if (FilePath.Contains(PathFolderHelper.ProjectFileExtension))
        {
            FilePath = Path.GetDirectoryName(FilePath);
        }

        var baseDir = !string.IsNullOrWhiteSpace(fileSystemPath) ? fileSystemPath : FilePath!;
        FilePath = baseDir;

        // Persist the single local output root so all upload targets reuse the same staging layout.
        Model.FileSystemBasePath = baseDir;

        // Build output directories
        Model.PackageFilesOutputPath = Path.Combine(baseDir, PathFolderHelper.PackageFilesDirectory);
        Model.VelopackOutputPath = Path.Combine(baseDir, PathFolderHelper.ReleasesDirectory);

        Directory.CreateDirectory(FilePath!);
        Directory.CreateDirectory(Model.PackageFilesOutputPath);
        Directory.CreateDirectory(Model.VelopackOutputPath);
        CleanLegacyReleaseArtifactsFromRoot(baseDir);

        var asProj = Path.Combine(FilePath!, $"{Model.AppId}{PathFolderHelper.ProjectFileExtension}");

        // Serialize with a resolver that ignores non-persistable/runtime properties
        File.WriteAllText(asProj, JsonSerializer.Serialize(Model, _saveOptions));
        Trace.WriteLine("FILE SAVED ! : " + FilePath);

        _isSaved = true;

        // reset dirty flags
        _hasUnsavedNonTreeChanges = false;
        Model.HasUnsavedTreeChanges = false;
        this.RaisePropertyChanged(nameof(HasUnsavedChanges));

        AddLastProject(asProj);
        this.RaisePropertyChanged(nameof(WindowTitle));
    }

    /// <summary>
    /// Saves as.
    /// </summary>
    [ReactiveCommand]
    public void SaveAs()
    {
        var previousFilePath = FilePath;

        try
        {
            var saveFileDialog = new System.Windows.Forms.FolderBrowserDialog
            {
                SelectedPath = PathFolderHelper.GetMyDirectory(MyDirectory.Project),
                ShowNewFolderButton = true
            };

            if (saveFileDialog.ShowDialog() != System.Windows.Forms.DialogResult.OK)
            {
                return;
            }

            FilePath = saveFileDialog.SelectedPath;

            Save();
        }
        catch (Exception)
        {
            MessageBox.Show("Error on saving");

            FilePath = previousFilePath;
        }
    }

    /// <summary>
    /// Releases unmanaged and - optionally - managed resources.
    /// </summary>
    /// <param name="disposing"><c>true</c> to release both managed and unmanaged resources; <c>false</c> to release only
    /// unmanaged resources.</param>
    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing)
        {
            _activeBackgroungWorker?.Dispose();
            _exeProcess?.Dispose();
            _dirtySubscription?.Dispose();
        }
    }

    private void SetupDirtyTracking()
    {
        _dirtySubscription?.Dispose();
        if (Model == null)
        {
            return;
        }

        var dirtyStreams = new IObservable<Unit>[]
        {
            Model.WhenAnyValue(m => m.Title).Select(_ => Unit.Default),
            Model.WhenAnyValue(m => m.Authors).Select(_ => Unit.Default),
            Model.WhenAnyValue(m => m.Description).Select(_ => Unit.Default),
            Model.WhenAnyValue(m => m.Version).Select(_ => Unit.Default),
            Model.WhenAnyValue(m => m.AppId).Select(_ => Unit.Default),
            Model.WhenAnyValue(m => m.MainExeName).Select(_ => Unit.Default),
            Model.WhenAnyValue(m => m.IconFilepath).Select(_ => Unit.Default),
            Model.WhenAnyValue(m => m.SplashFilepath).Select(_ => Unit.Default),
            Model.WhenAnyValue(m => m.SelectedConnectionString).Select(_ => Unit.Default),
            Model.WhenAnyValue(m => m.PackageFilesOutputPath).Select(_ => Unit.Default),
            Model.WhenAnyValue(m => m.VelopackOutputPath).Select(_ => Unit.Default),
            Model.WhenAnyValue(m => m.FileSystemBasePath).Select(_ => Unit.Default),
            Model.WhenAnyValue(m => m.Channel).Select(_ => Unit.Default),
            Model.WhenAnyValue(m => m.Runtime).Select(_ => Unit.Default),
            Model.WhenAnyValue(m => m.ReleaseNotesPath).Select(_ => Unit.Default),
            Model.WhenAnyValue(m => m.DeltaMode).Select(_ => Unit.Default),
            Model.WhenAnyValue(m => m.ExcludeRegex).Select(_ => Unit.Default),
            Model.WhenAnyValue(m => m.NoPortable).Select(_ => Unit.Default),
            Model.WhenAnyValue(m => m.NoInstaller).Select(_ => Unit.Default),
            Model.WhenAnyValue(m => m.Frameworks).Select(_ => Unit.Default),
            Model.WhenAnyValue(m => m.SkipVeloAppCheck).Select(_ => Unit.Default),
            Model.WhenAnyValue(m => m.Shortcuts).Select(_ => Unit.Default),
            Model.WhenAnyValue(m => m.SignParams).Select(_ => Unit.Default),
            Model.WhenAnyValue(m => m.SignTemplate).Select(_ => Unit.Default),
            Model.WhenAnyValue(m => m.SignExclude).Select(_ => Unit.Default),
            Model.WhenAnyValue(m => m.SignParallel).Select(_ => Unit.Default),
            Model.WhenAnyValue(m => m.AzureTrustedSignFile).Select(_ => Unit.Default),
            Model.WhenAnyValue(m => m.MsiDeploymentTool).Select(_ => Unit.Default),
            Model.WhenAnyValue(m => m.MsiDeploymentToolVersion).Select(_ => Unit.Default)
        };
        _dirtySubscription = Observable.Merge(dirtyStreams)
            .Subscribe(_ => _hasUnsavedNonTreeChanges = true);
    }

    private void ActiveBackgroungWorker_DoWork(object? sender, DoWorkEventArgs e)
    {
        try
        {
            _activeBackgroungWorker?.ReportProgress(20, "VELOPACK PACKAGE CREATING");

            if (_activeBackgroungWorker?.CancellationPending == true)
            {
                return;
            }

            if (Model?.PackageFilesOutputPath == null)
            {
                throw new Exception("PackageFilesOutputPath is null");
            }

            // Recreate the staging directory from the original source paths every time.
            Model.ClearPackageFilesDirectory();

            // Copy all selected items preserving folder structure
            void CopyNode(ItemLink node, List<string> parents)
            {
                if (_activeBackgroungWorker?.CancellationPending == true)
                {
                    return;
                }

                if (node.IsDirectory)
                {
                    var dirName = string.IsNullOrWhiteSpace(node.Filename) ? "Folder" : node.Filename;
                    var newParents = new List<string>(parents) { dirName };
                    foreach (var child in node.Children)
                    {
                        CopyNode(child, newParents);
                    }
                }
                else
                {
                    if (string.IsNullOrWhiteSpace(node.SourceFilepath) || !File.Exists(node.SourceFilepath))
                    {
                        return;
                    }

                    var relDir = Path.Combine([.. parents]);
                    var targetDir = string.IsNullOrEmpty(relDir) ? Model!.PackageFilesOutputPath : Path.Combine(Model!.PackageFilesOutputPath, relDir);
                    Directory.CreateDirectory(targetDir);
                    var targetFile = Path.Combine(targetDir, node.Filename);
                    File.Copy(node.SourceFilepath, targetFile, true);
                }
            }

            _activeBackgroungWorker?.ReportProgress(40, "COPYING CONTENT");
            foreach (var node in Model!.PackageFiles.ToList())
            {
                CopyNode(node, []);
            }

            _activeBackgroungWorker?.ReportProgress(60, "VELOPACK RELEASIFY");

            VelopackPack();
            Trace.WriteLine("CREATED VELOPACK PACKAGE to : " + Model.VelopackOutputPath);
        }
        catch (Exception ex)
        {
            e.Result = ex;
        }
    }

    private void ActiveBackgroungWorker_ProgressChanged(object? sender, ProgressChangedEventArgs e)
    {
        // TODO : Update busy indicator information.
        if (e.UserState is not string message)
        {
            return;
        }

        CurrentPackageCreationStage = message;
    }

    private void AddLastProject(string filePath)
    {
        foreach (var fp in UserPreference.LastOpenedProject.Where(p => string.Equals(p, filePath, StringComparison.CurrentCultureIgnoreCase)).ToList())
        {
            UserPreference.LastOpenedProject.Remove(fp);
        }

        UserPreference.LastOpenedProject.Add(filePath);

        PathFolderHelper.SavePreference(UserPreference);
    }

    /// <summary>
    /// Called on package created. Start the upload.
    /// </summary>
    /// <param name="sender">The sender.</param>
    /// <param name="e">The <see cref="RunWorkerCompletedEventArgs"/> instance containing the event data.</param>
    private void PackageCreationCompleted(object? sender, RunWorkerCompletedEventArgs e)
    {
        IsBusy = false;

        CurrentPackageCreationStage = string.Empty;

        _activeBackgroungWorker?.Dispose();

        _activeBackgroungWorker = null;

        if (_abortPackageFlag)
        {
            Model?.UploadQueue?.Clear();

            _abortPackageFlag = false;

            return;
        }

        if (e.Result is Exception ex)
        {
            MessageBox.Show(ex.Message, "Package creation error", MessageBoxButton.OK, MessageBoxImage.Error);

            // TODO : Manage generated error
            return;
        }

        if (e.Cancelled)
        {
            return;
        }

        // Start uploading generated files.
        Model?.BeginUpdatedFiles(_publishMode);
    }

    private void VelopackPack()
    {
        if (Model == null)
        {
            throw new Exception("Model is null");
        }

        var packDir = Path.GetFullPath(Model.PackageFilesOutputPath!);
        var outDir = Path.GetFullPath(Model.VelopackOutputPath!);
        var startInfo = CreateVelopackPackStartInfo(packDir, outDir);

        using (_exeProcess = Process.Start(startInfo))
        {
            var stdout = _exeProcess!.StandardOutput.ReadToEnd();
            var stderr = _exeProcess!.StandardError.ReadToEnd();
            _exeProcess!.WaitForExit();
            Trace.WriteLine(stdout);
            if (!string.IsNullOrWhiteSpace(stderr))
            {
                Trace.TraceError(stderr);
            }

            if (_exeProcess!.ExitCode != 0)
            {
                throw new Exception($"vpk exited with code {_exeProcess.ExitCode}.\n{stderr}\n{stdout}");
            }
        }
    }

    private void CleanLegacyReleaseArtifactsFromRoot(string baseDir)
    {
        if (Model == null || string.IsNullOrWhiteSpace(Model.AppId) || string.IsNullOrWhiteSpace(baseDir) || !Directory.Exists(baseDir))
        {
            return;
        }

        var patterns = new[]
        {
            "assets.*.json",
            "releases.*.json",
            "RELEASES",
            $"{Model.AppId}-*.nupkg",
            $"{Model.AppId}-*.zip",
            $"{Model.AppId}-*.exe",
            $"{Model.AppId}-*.msi"
        };

        foreach (var pattern in patterns)
        {
            foreach (var file in Directory.EnumerateFiles(baseDir, pattern, SearchOption.TopDirectoryOnly))
            {
                TryDeleteFile(file);
            }
        }

        static void TryDeleteFile(string file)
        {
            try
            {
                File.SetAttributes(file, FileAttributes.Normal);
                File.Delete(file);
            }
            catch
            {
                // Ignore cleanup failures; vpk will still write the correct Releases output.
            }
        }
    }

    private ProcessStartInfo CreateVelopackPackStartInfo(string packDir, string outDir)
    {
        if (Model == null)
        {
            throw new Exception("Model is null");
        }

        var startInfo = new ProcessStartInfo
        {
            WindowStyle = ProcessWindowStyle.Hidden,
            FileName = "vpk",
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };

        var args = startInfo.ArgumentList;
        args.Add("pack");

        AddOption(args, "--packId", Model.AppId);
        AddOption(args, "--packVersion", Model.Version);
        AddOption(args, "--packDir", packDir);
        AddOption(args, "--outputDir", outDir);
        AddOption(args, "--channel", Model.Channel);
        AddOption(args, "--runtime", Model.Runtime);
        AddOption(args, "--packAuthors", Model.Authors);
        AddOption(args, "--packTitle", Model.Title);
        AddOption(args, "--releaseNotes", Model.ReleaseNotesPath);
        AddOption(args, "--delta", Model.GetResolvedDeltaMode());

        if (File.Exists(Model.IconFilepath))
        {
            AddOption(args, "--icon", Path.GetFullPath(Model.IconFilepath));
        }

        var mainExe = Model.MainExeName;
        if (string.IsNullOrWhiteSpace(mainExe) && !string.IsNullOrWhiteSpace(Model.MainExePath))
        {
            mainExe = Path.GetFileName(Model.MainExePath);
        }

        AddOption(args, "--mainExe", mainExe);
        AddOption(args, "--exclude", Model.ExcludeRegex);
        AddFlag(args, "--noPortable", Model.NoPortable);
        AddFlag(args, "--noInst", Model.NoInstaller);
        AddOption(args, "--framework", Model.Frameworks);

        if (File.Exists(Model.SplashFilepath))
        {
            AddOption(args, "--splashImage", Path.GetFullPath(Model.SplashFilepath));
        }

        AddFlag(args, "--skipVeloAppCheck", Model.SkipVeloAppCheck);
        AddOption(args, "--signTemplate", Model.SignTemplate);
        AddOption(args, "--signExclude", Model.SignExclude);

        if (Model.SignParallel != 10)
        {
            AddOption(args, "--signParallel", Model.SignParallel.ToString(CultureInfo.InvariantCulture));
        }

        AddOption(args, "--shortcuts", Model.Shortcuts);
        AddOption(args, "--signParams", Model.SignParams);
        AddOption(args, "--azureTrustedSignFile", Model.AzureTrustedSignFile);
        AddFlag(args, "--msiDeploymentTool", Model.MsiDeploymentTool);
        AddOption(args, "--msiDeploymentToolVersion", Model.MsiDeploymentToolVersion);

        Trace.WriteLine("vpk " + string.Join(" ", args.Select(FormatArgumentForTrace)));
        return startInfo;

        static void AddFlag(ICollection<string> args, string option, bool enabled)
        {
            if (enabled)
            {
                args.Add(option);
            }
        }

        static void AddOption(ICollection<string> args, string option, string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return;
            }

            args.Add(option);
            args.Add(value);
        }

        static string FormatArgumentForTrace(string argument) =>
            argument.Contains(' ', StringComparison.Ordinal) ? '"' + argument + '"' : argument;
    }
}
