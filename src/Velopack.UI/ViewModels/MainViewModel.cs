// <copyright file="MainViewModel.cs" company="Chris Pulman">
// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.
// </copyright>

using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Runtime.Versioning;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using System.Windows;
using CrissCross;
using Microsoft.Win32;
using ReactiveUI;
using ReactiveUI.Primitives;
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
    private readonly JsonSerializerOptions saveOptions;
    private BackgroundWorker? activeBackgroungWorker;
    private bool abortPackageFlag;
    [Reactive]
    private string? currentPackageCreationStage;
    [Reactive]
    private bool isBusy;
    [Reactive]
    private VelopackModel? model;
    private bool isSaved;
    private int publishMode;
    private Process? exeProcess;
    private string? filePath;
    private IDisposable? dirtySubscription;
    private bool hasUnsavedNonTreeChanges;

    /// <summary>
    /// Initializes a new instance of the <see cref="MainViewModel"/> class.
    /// </summary>
    public MainViewModel()
    {
        this.saveOptions = new JsonSerializerOptions
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
                                nameof(VelopackModel.HasUnsavedTreeChanges),
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
                                nameof(ItemLink.HasDummyChild),
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
                                },
                            };
                        }
                    },
                },
            },
        };

        this.Model = new VelopackModel();
        this.SetupDirtyTracking();

        this.UserPreference = PathFolderHelper.LoadUserPreference();

        var startupProject = App.StartupProjectFilePath;
        var last = string.IsNullOrWhiteSpace(startupProject)
            ? this.UserPreference.LastOpenedProject.LastOrDefault()
            : startupProject;

        if (!string.IsNullOrEmpty(last) && File.Exists(last))
        {
            this.OpenProject(last);
        }

        this.AbortPackageCreationCmd = ReactiveCommand.Create(this.AbortPackageCreation);
    }

    /// <summary>
    /// Gets a value indicating whether this instance has unsaved changes.
    /// </summary>
    /// <value>
    ///   <c>true</c> if this instance has unsaved changes; otherwise, <c>false</c>.
    /// </value>
    public bool HasUnsavedChanges => (this.Model?.HasUnsavedTreeChanges ?? false) || this.hasUnsavedNonTreeChanges || string.IsNullOrWhiteSpace(this.FilePath);

    /// <summary>
    /// Gets the abort package creation command.
    /// </summary>
    /// <value>The abort package creation command.</value>
    public ReactiveCommand<RxVoid, RxVoid> AbortPackageCreationCmd { get; }

    /// <summary>
    /// Gets or sets the file path.
    /// </summary>
    /// <value>The file path.</value>
    public string? FilePath
    {
        get => this.filePath;

        set
        {
            this.Model?.CurrentFilePath = value;

            this.RaiseAndSetIfChanged(ref this.filePath, value);
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
            if (!string.IsNullOrWhiteSpace(this.FilePath))
            {
                fp = Path.GetFileNameWithoutExtension(this.FilePath);
            }

            return $"{PathFolderHelper.ProgramName} {PathFolderHelper.GetProgramVersion()} - {fp}";
        }
    }

    /// <summary>
    /// Aborts the package creation.
    /// </summary>
    public void AbortPackageCreation()
    {
        if (this.activeBackgroungWorker != null)
        {
            this.activeBackgroungWorker.CancelAsync();

            this.exeProcess?.Kill();
        }

        this.abortPackageFlag = true;
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
            this.Save();
        }

        this.Model = new VelopackModel();
        this.SetupDirtyTracking();
        this.hasUnsavedNonTreeChanges = false;
        this.Model.HasUnsavedTreeChanges = false;
        this.RaisePropertyChanged(nameof(this.HasUnsavedChanges));
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
                Multiselect = false,
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

            this.OpenProject(ofd.FileName);
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

            this.FilePath = filepath;

            var m = JsonSerializer.Deserialize<VelopackModel>(File.ReadAllText(filepath), this.saveOptions);

            if (m == null)
            {
                return;
            }

            this.Model = m;

            // Resync connection instances and mirror saved FileSystemBasePath into the active FileSystemConnection
            this.Model.ResyncAfterLoad();
            if (this.Model.SelectedConnection is not FileSystemConnection)
            {
                var projectDir = Path.GetDirectoryName(filepath);
                if (!string.IsNullOrWhiteSpace(projectDir))
                {
                    this.Model.SetOutputBasePath(projectDir);
                }
            }

            this.SetupDirtyTracking();
            this.Model.PackageFiles = VelopackModel.OrderFileList(this.Model.PackageFiles);
            this.Model.RefreshPackageVersion();
            this.AddLastProject(filepath);
            this.RaisePropertyChanged(nameof(this.WindowTitle));

            // opened from disk -> clean dirty flags
            this.hasUnsavedNonTreeChanges = false;
            this.Model.HasUnsavedTreeChanges = false;
            this.RaisePropertyChanged(nameof(this.HasUnsavedChanges));
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
            if (this.activeBackgroungWorker?.IsBusy == true)
            {
                Trace.TraceError("You shouldn't be here !");
                return;
            }

            this.Model?.UploadQueue.Clear();
            this.Model?.RefreshPackageVersion();

            Trace.WriteLine("START PUBLISHING ! : " + this.Model?.Title);

            // 1) Check validity
            if (this.Model?.IsValid == false)
            {
                throw new InvalidOperationException("Package Details are invalid or incomplete !");
            }

            if (this.Model?.SelectedConnection == null || !this.Model.SelectedConnection.IsValid)
            {
                throw new InvalidOperationException("Selected connection details are not valid !");
            }

            Trace.WriteLine("DATA VALIDATE - OK ! ");

            this.Save();

            // I proceed only if i created the project .velo file and directory I need existing
            // directory to create the packages.
            if (!this.isSaved)
            {
                return;
            }

            this.IsBusy = true;

            this.activeBackgroungWorker = new BackgroundWorker() { WorkerReportsProgress = true, WorkerSupportsCancellation = true };

            this.activeBackgroungWorker.DoWork += this.ActiveBackgroungWorker_DoWork;
            this.activeBackgroungWorker.RunWorkerCompleted += this.PackageCreationCompleted;
            this.activeBackgroungWorker.ProgressChanged += this.ActiveBackgroungWorker_ProgressChanged;

            this.activeBackgroungWorker.RunWorkerAsync(this);
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
        this.publishMode = 0;
        this.PublishPackage();
    }

    /// <summary>
    /// Publishes the package only update.
    /// </summary>
    [ReactiveCommand]
    public void PublishPackageOnlyUpdate()
    {
        this.publishMode = 1;
        this.PublishPackage();
    }

    /// <summary>
    /// Saves this instance.
    /// </summary>
    [ReactiveCommand]
    public void Save()
    {
        if (this.Model == null)
        {
            return;
        }

        var fileSystemPath = this.Model.SelectedConnection is FileSystemConnection fsc && !string.IsNullOrWhiteSpace(fsc.FileSystemPath)
            ? fsc.FileSystemPath
            : null;

        if (string.IsNullOrWhiteSpace(this.FilePath))
        {
            if (!string.IsNullOrWhiteSpace(fileSystemPath))
            {
                this.FilePath = fileSystemPath;
            }
            else
            {
                this.SaveAs();
                return;
            }
        }

        if (this.FilePath.Contains(PathFolderHelper.ProjectFileExtension))
        {
            this.FilePath = Path.GetDirectoryName(this.FilePath);
        }

        var baseDir = !string.IsNullOrWhiteSpace(fileSystemPath) ? fileSystemPath : this.FilePath!;
        this.FilePath = baseDir;

        // Persist the single local output root so all upload targets reuse the same staging layout.
        this.Model.FileSystemBasePath = baseDir;

        // Build output directories
        this.Model.PackageFilesOutputPath = Path.Combine(baseDir, PathFolderHelper.PackageFilesDirectory);
        this.Model.VelopackOutputPath = Path.Combine(baseDir, PathFolderHelper.ReleasesDirectory);

        Directory.CreateDirectory(this.FilePath!);
        Directory.CreateDirectory(this.Model.PackageFilesOutputPath);
        Directory.CreateDirectory(this.Model.VelopackOutputPath);
        this.CleanLegacyReleaseArtifactsFromRoot(baseDir);

        var asProj = Path.Combine(this.FilePath!, $"{this.Model.AppId}{PathFolderHelper.ProjectFileExtension}");

        // Serialize with a resolver that ignores non-persistable/runtime properties
        File.WriteAllText(asProj, JsonSerializer.Serialize(this.Model, this.saveOptions));
        Trace.WriteLine("FILE SAVED ! : " + this.FilePath);

        this.isSaved = true;

        // reset dirty flags
        this.hasUnsavedNonTreeChanges = false;
        this.Model.HasUnsavedTreeChanges = false;
        this.RaisePropertyChanged(nameof(this.HasUnsavedChanges));

        this.AddLastProject(asProj);
        this.RaisePropertyChanged(nameof(this.WindowTitle));
    }

    /// <summary>
    /// Saves as.
    /// </summary>
    [ReactiveCommand]
    public void SaveAs()
    {
        var previousFilePath = this.FilePath;

        try
        {
            var saveFileDialog = new System.Windows.Forms.FolderBrowserDialog
            {
                SelectedPath = PathFolderHelper.GetMyDirectory(MyDirectory.Project),
                ShowNewFolderButton = true,
            };

            if (saveFileDialog.ShowDialog() != System.Windows.Forms.DialogResult.OK)
            {
                return;
            }

            this.FilePath = saveFileDialog.SelectedPath;

            this.Save();
        }
        catch (Exception)
        {
            MessageBox.Show("Error on saving");

            this.FilePath = previousFilePath;
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
        if (!disposing)
        {
            return;
        }

        this.activeBackgroungWorker?.Dispose();
        this.exeProcess?.Dispose();
        this.dirtySubscription?.Dispose();
    }

    private void SetupDirtyTracking()
    {
        this.dirtySubscription?.Dispose();
        if (this.Model == null)
        {
            return;
        }

        var dirtyStreams = new IObservable<RxVoid>[]
        {
            this.Model.WhenAnyValue(m => m.Title).Select(_ => RxVoid.Default),
            this.Model.WhenAnyValue(m => m.Authors).Select(_ => RxVoid.Default),
            this.Model.WhenAnyValue(m => m.Description).Select(_ => RxVoid.Default),
            this.Model.WhenAnyValue(m => m.Version).Select(_ => RxVoid.Default),
            this.Model.WhenAnyValue(m => m.AppId).Select(_ => RxVoid.Default),
            this.Model.WhenAnyValue(m => m.MainExeName).Select(_ => RxVoid.Default),
            this.Model.WhenAnyValue(m => m.IconFilepath).Select(_ => RxVoid.Default),
            this.Model.WhenAnyValue(m => m.SplashFilepath).Select(_ => RxVoid.Default),
            this.Model.WhenAnyValue(m => m.SelectedConnectionString).Select(_ => RxVoid.Default),
            this.Model.WhenAnyValue(m => m.PackageFilesOutputPath).Select(_ => RxVoid.Default),
            this.Model.WhenAnyValue(m => m.VelopackOutputPath).Select(_ => RxVoid.Default),
            this.Model.WhenAnyValue(m => m.FileSystemBasePath).Select(_ => RxVoid.Default),
            this.Model.WhenAnyValue(m => m.Channel).Select(_ => RxVoid.Default),
            this.Model.WhenAnyValue(m => m.Runtime).Select(_ => RxVoid.Default),
            this.Model.WhenAnyValue(m => m.ReleaseNotesPath).Select(_ => RxVoid.Default),
            this.Model.WhenAnyValue(m => m.DeltaMode).Select(_ => RxVoid.Default),
            this.Model.WhenAnyValue(m => m.ExcludeRegex).Select(_ => RxVoid.Default),
            this.Model.WhenAnyValue(m => m.NoPortable).Select(_ => RxVoid.Default),
            this.Model.WhenAnyValue(m => m.NoInstaller).Select(_ => RxVoid.Default),
            this.Model.WhenAnyValue(m => m.Frameworks).Select(_ => RxVoid.Default),
            this.Model.WhenAnyValue(m => m.SkipVeloAppCheck).Select(_ => RxVoid.Default),
            this.Model.WhenAnyValue(m => m.Shortcuts).Select(_ => RxVoid.Default),
            this.Model.WhenAnyValue(m => m.SignParams).Select(_ => RxVoid.Default),
            this.Model.WhenAnyValue(m => m.SignTemplate).Select(_ => RxVoid.Default),
            this.Model.WhenAnyValue(m => m.UseSimplySign).Select(_ => RxVoid.Default),
            this.Model.WhenAnyValue(m => m.SimplySignExecutablePath).Select(_ => RxVoid.Default),
            this.Model.WhenAnyValue(m => m.SimplySignEmail).Select(_ => RxVoid.Default),
            this.Model.WhenAnyValue(m => m.SignExclude).Select(_ => RxVoid.Default),
            this.Model.WhenAnyValue(m => m.SignParallel).Select(_ => RxVoid.Default),
            this.Model.WhenAnyValue(m => m.AzureTrustedSignFile).Select(_ => RxVoid.Default),
            this.Model.WhenAnyValue(m => m.MsiDeploymentTool).Select(_ => RxVoid.Default),
            this.Model.WhenAnyValue(m => m.MsiDeploymentToolVersion).Select(_ => RxVoid.Default),
        };
        this.dirtySubscription = dirtyStreams.Merge()
            .Subscribe(_ => this.hasUnsavedNonTreeChanges = true);
    }

    private void ActiveBackgroungWorker_DoWork(object? sender, DoWorkEventArgs e)
    {
        try
        {
            this.activeBackgroungWorker?.ReportProgress(20, "VELOPACK PACKAGE CREATING");

            if (this.activeBackgroungWorker?.CancellationPending == true)
            {
                return;
            }

            if (this.Model?.PackageFilesOutputPath == null)
            {
                throw new InvalidOperationException("PackageFilesOutputPath is null");
            }

            // Recreate the staging directory from the original source paths every time.
            this.Model.ClearPackageFilesDirectory();

            // Copy all selected items preserving folder structure
            void CopyNode(ItemLink node, List<string> parents)
            {
                if (this.activeBackgroungWorker?.CancellationPending == true)
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
                    var targetDir = string.IsNullOrEmpty(relDir) ? this.Model!.PackageFilesOutputPath : Path.Combine(this.Model!.PackageFilesOutputPath, relDir);
                    Directory.CreateDirectory(targetDir);
                    var targetFile = Path.Combine(targetDir, node.Filename);
                    File.Copy(node.SourceFilepath, targetFile, true);
                }
            }

            this.activeBackgroungWorker?.ReportProgress(40, "COPYING CONTENT");
            foreach (var node in this.Model!.PackageFiles.ToList())
            {
                CopyNode(node, []);
            }

            this.activeBackgroungWorker?.ReportProgress(60, "VELOPACK RELEASIFY");

            this.VelopackPack();
            Trace.WriteLine("CREATED VELOPACK PACKAGE to : " + this.Model.VelopackOutputPath);
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

        this.CurrentPackageCreationStage = message;
    }

    private void AddLastProject(string filePath)
    {
        foreach (var fp in this.UserPreference.LastOpenedProject.Where(p => string.Equals(p, filePath, StringComparison.CurrentCultureIgnoreCase)).ToList())
        {
            this.UserPreference.LastOpenedProject.Remove(fp);
        }

        this.UserPreference.LastOpenedProject.Add(filePath);

        PathFolderHelper.SavePreference(this.UserPreference);
    }

    /// <summary>
    /// Called on package created. Start the upload.
    /// </summary>
    /// <param name="sender">The sender.</param>
    /// <param name="e">The <see cref="RunWorkerCompletedEventArgs"/> instance containing the event data.</param>
    private void PackageCreationCompleted(object? sender, RunWorkerCompletedEventArgs e)
    {
        this.IsBusy = false;

        this.CurrentPackageCreationStage = string.Empty;

        this.activeBackgroungWorker?.Dispose();

        this.activeBackgroungWorker = null;

        if (this.abortPackageFlag)
        {
            this.Model?.UploadQueue?.Clear();

            this.abortPackageFlag = false;

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
        this.Model?.BeginUpdatedFiles(this.publishMode);
    }

    private void VelopackPack()
    {
        if (this.Model == null)
        {
            throw new InvalidOperationException("Model is null");
        }

        var packDir = Path.GetFullPath(this.Model.PackageFilesOutputPath!);
        var outDir = Path.GetFullPath(this.Model.VelopackOutputPath!);
        var startInfo = this.CreateVelopackPackStartInfo(packDir, outDir);

        using (this.exeProcess = Process.Start(startInfo))
        {
            var stdout = this.exeProcess!.StandardOutput.ReadToEnd();
            var stderr = this.exeProcess!.StandardError.ReadToEnd();
            this.exeProcess!.WaitForExit();
            Trace.WriteLine(stdout);
            if (!string.IsNullOrWhiteSpace(stderr))
            {
                Trace.TraceError(stderr);
            }

            if (this.exeProcess!.ExitCode != 0)
            {
                throw new InvalidOperationException($"vpk exited with code {this.exeProcess.ExitCode}.\n{stderr}\n{stdout}");
            }
        }
    }

    private void CleanLegacyReleaseArtifactsFromRoot(string baseDir)
    {
        if (this.Model == null || string.IsNullOrWhiteSpace(this.Model.AppId) || string.IsNullOrWhiteSpace(baseDir) || !Directory.Exists(baseDir))
        {
            return;
        }

        var patterns = new[]
        {
            "assets.*.json",
            "releases.*.json",
            "RELEASES",
            $"{this.Model.AppId}-*.nupkg",
            $"{this.Model.AppId}-*.zip",
            $"{this.Model.AppId}-*.exe",
            $"{this.Model.AppId}-*.msi",
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
        if (this.Model == null)
        {
            throw new InvalidOperationException("Model is null");
        }

        var startInfo = new ProcessStartInfo
        {
            WindowStyle = ProcessWindowStyle.Hidden,
            FileName = "vpk",
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
        };

        var args = startInfo.ArgumentList;
        args.Add("pack");

        AddOption(args, "--packId", this.Model.AppId);
        AddOption(args, "--packVersion", this.Model.Version);
        AddOption(args, "--packDir", packDir);
        AddOption(args, "--outputDir", outDir);
        AddOption(args, "--channel", this.Model.Channel);
        AddOption(args, "--runtime", this.Model.Runtime);
        AddOption(args, "--packAuthors", this.Model.Authors);
        AddOption(args, "--packTitle", this.Model.Title);
        AddOption(args, "--releaseNotes", this.Model.ReleaseNotesPath);
        AddOption(args, "--delta", this.Model.GetResolvedDeltaMode());

        if (File.Exists(this.Model.IconFilepath))
        {
            AddOption(args, "--icon", Path.GetFullPath(this.Model.IconFilepath));
        }

        var mainExe = this.Model.MainExeName;
        if (string.IsNullOrWhiteSpace(mainExe) && !string.IsNullOrWhiteSpace(this.Model.MainExePath))
        {
            mainExe = Path.GetFileName(this.Model.MainExePath);
        }

        AddOption(args, "--mainExe", mainExe);
        AddOption(args, "--exclude", this.Model.ExcludeRegex);
        AddFlag(args, "--noPortable", this.Model.NoPortable);
        AddFlag(args, "--noInst", this.Model.NoInstaller);
        AddOption(args, "--framework", this.Model.Frameworks);

        if (File.Exists(this.Model.SplashFilepath))
        {
            AddOption(args, "--splashImage", Path.GetFullPath(this.Model.SplashFilepath));
        }

        AddFlag(args, "--skipVeloAppCheck", this.Model.SkipVeloAppCheck);
        AddOption(args, "--signTemplate", this.Model.UseSimplySign ? SimplySignSigning.Configure(startInfo, this.Model) : this.Model.SignTemplate);
        AddOption(args, "--signExclude", this.Model.SignExclude);

        if (this.Model.UseSimplySign || this.Model.SignParallel != 10)
        {
            AddOption(args, "--signParallel", this.Model.UseSimplySign ? "1" : this.Model.SignParallel.ToString(CultureInfo.InvariantCulture));
        }

        AddOption(args, "--shortcuts", this.Model.Shortcuts);
        AddOption(args, "--signParams", this.Model.SignParams);
        AddOption(args, "--azureTrustedSignFile", this.Model.AzureTrustedSignFile);
        AddFlag(args, "--msiDeploymentTool", this.Model.MsiDeploymentTool);
        AddOption(args, "--msiDeploymentToolVersion", this.Model.MsiDeploymentToolVersion);

        Trace.WriteLine("vpk " + string.Join(" ", args.Select(FormatArgumentForTrace)));
        return startInfo;

        static void AddFlag(ICollection<string> args, string option, bool enabled)
        {
            if (!enabled)
            {
                return;
            }

            args.Add(option);
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
