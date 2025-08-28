using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reactive;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using System.Windows;
using CrissCross;
using Microsoft.Win32;
using ReactiveUI;
using ReactiveUI.SourceGenerators;

namespace Velopack.UI;

public partial class MainViewModel : RxObject
{
    internal BackgroundWorker? ActiveBackgroungWorker;
    private bool _abortPackageFlag;
    [Reactive]
    private string? _currentPackageCreationStage;
    [Reactive]
    private bool _isBusy;
    [Reactive]
    private AutoSquirrelModel? _model;
    private bool _isSaved;
    private int _publishMode;
    private Process? _exeProcess;
    private string? _filePath;

    public MainViewModel()
    {
        Model = new AutoSquirrelModel();

        UserPreference = PathFolderHelper.LoadUserPreference();

        var last = UserPreference.LastOpenedProject.LastOrDefault();

        if (!string.IsNullOrEmpty(last) && File.Exists(last))
        {
            OpenProject(last);
        }

        AbortPackageCreationCmd = ReactiveCommand.Create(() => AbortPackageCreation());
    }

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
            if (Model != null)
            {
                Model.CurrentFilePath = value;
            }
            
            this.RaiseAndSetIfChanged(ref _filePath, value);
        }
    }

    /// <summary>
    /// The user preference
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
            var fp = "New Project" + "*";
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
        if (ActiveBackgroungWorker != null)
        {
            ActiveBackgroungWorker.CancelAsync();

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

        Model = new AutoSquirrelModel();
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

            var m = JsonSerializer.Deserialize<AutoSquirrelModel>(File.ReadAllText(filepath));

            if (m == null)
            {
                return;
            }

            Model = m;
            Model.PackageFiles = AutoSquirrelModel.OrderFileList(Model.PackageFiles);
            Model.RefreshPackageVersion();
            AddLastProject(filepath);
            this.RaisePropertyChanged(nameof(WindowTitle));
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
    /// Package Details are invalid or incomplete ! or Selected connection details are not valid !
    /// </exception>
    public void PublishPackage()
    {
        try
        {
            if (ActiveBackgroungWorker?.IsBusy == true)
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

            ActiveBackgroungWorker = new BackgroundWorker() { WorkerReportsProgress = true, WorkerSupportsCancellation = true };

            ActiveBackgroungWorker.DoWork += ActiveBackgroungWorker_DoWork;
            ActiveBackgroungWorker.RunWorkerCompleted += PackageCreationCompleted;
            ActiveBackgroungWorker.ProgressChanged += ActiveBackgroungWorker_ProgressChanged;

            ActiveBackgroungWorker.RunWorkerAsync(this);
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
    ///   end of visual studio release build
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
        if (string.IsNullOrWhiteSpace(FilePath))
        {
            SaveAs();
            return;
        }
        if (FilePath.Contains(PathFolderHelper.ProjectFileExtension))
        {
            FilePath = Path.GetDirectoryName(FilePath);
        }

        if (Model == null)
        {
            return;
        }

        // If FileSystem connection is selected with a path, prefer it for SquirrelOutputPath
        string baseDir;
        if (Model.SelectedConnection is FileSystemConnection fsc && !string.IsNullOrWhiteSpace(fsc.FileSystemPath))
        {
            baseDir = fsc.FileSystemPath;
        }
        else
        {
            baseDir = Path.Combine(FilePath!, Model.AppId + "_files");
        }

        // Build output directories
        Model.NupkgOutputPath = Path.Combine(baseDir, PathFolderHelper.PackageDirectory);
        Model.SquirrelOutputPath = Path.Combine(baseDir, PathFolderHelper.ReleasesDirectory);

        Directory.CreateDirectory(Model.NupkgOutputPath);
        Directory.CreateDirectory(Model.SquirrelOutputPath);

        var asProj = Path.Combine(FilePath!, $"{Model.AppId}{PathFolderHelper.ProjectFileExtension}");

        // Serialize with a resolver that ignores non-persistable/runtime properties
        var options = new JsonSerializerOptions
        {
            WriteIndented = false,
            TypeInfoResolver = new DefaultJsonTypeInfoResolver
            {
                Modifiers =
                {
                    ti =>
                    {
                        if (ti.Type == typeof(AutoSquirrelModel))
                        {
                            var namesToRemove = new HashSet<string>
                            {
                                nameof(AutoSquirrelModel.IconSource),
                                nameof(AutoSquirrelModel.SplashSource),
                                nameof(AutoSquirrelModel.SelectSplashCmd),
                                nameof(AutoSquirrelModel.SelectedLink),
                                nameof(AutoSquirrelModel.UploadQueue),
                                nameof(AutoSquirrelModel.SelectedConnection),
                                nameof(AutoSquirrelModel.SelectedUploadItem)
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
                    }
                }
            }
        };

        File.WriteAllText(asProj, JsonSerializer.Serialize(Model, options));
        Trace.WriteLine("FILE SAVED ! : " + FilePath);

        _isSaved = true;

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

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing)
        {
            ActiveBackgroungWorker?.Dispose();
            _exeProcess?.Dispose();
        }
    }

    private void ActiveBackgroungWorker_DoWork(object? sender, DoWorkEventArgs e)
    {
        try
        {
            ActiveBackgroungWorker?.ReportProgress(20, "VELOPACK PACKAGE CREATING");

            if (ActiveBackgroungWorker?.CancellationPending == true)
            {
                return;
            }

            if (Model?.NupkgOutputPath == null)
            {
                throw new Exception("NupkgOutputPath is null");
            }

            // Clean output content dir
            if (Directory.Exists(Model.NupkgOutputPath))
            {
                foreach (var f in Directory.EnumerateFiles(Model.NupkgOutputPath, "*", SearchOption.AllDirectories))
                {
                    try { File.SetAttributes(f, FileAttributes.Normal); File.Delete(f); } catch { }
                }
            }

            // Copy all selected items preserving folder structure
            void CopyNode(ItemLink node, List<string> parents)
            {
                if (ActiveBackgroungWorker?.CancellationPending == true)
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

                    var relDir = Path.Combine(parents.ToArray());
                    var targetDir = string.IsNullOrEmpty(relDir) ? Model!.NupkgOutputPath : Path.Combine(Model!.NupkgOutputPath, relDir);
                    Directory.CreateDirectory(targetDir);
                    var targetFile = Path.Combine(targetDir, node.Filename);
                    File.Copy(node.SourceFilepath, targetFile, true);
                }
            }

            ActiveBackgroungWorker?.ReportProgress(40, "COPYING CONTENT");
            foreach (var node in Model!.PackageFiles.ToList())
            {
                CopyNode(node, new List<string>());
            }

            ActiveBackgroungWorker?.ReportProgress(60, "VELOPACK RELEASIFY");

            VelopackPack();
            Trace.WriteLine("CREATED VELOPACK PACKAGE to : " + Model.SquirrelOutputPath);
        }
        catch (Exception ex)
        {
            e.Result = ex;
        }
    }

    private void ActiveBackgroungWorker_ProgressChanged(object? sender, ProgressChangedEventArgs e)
    {
        //todo : Update busy indicator information.
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
    /// <param name="sender"></param>
    /// <param name="e"></param>
    private void PackageCreationCompleted(object? sender, RunWorkerCompletedEventArgs e)
    {
        IsBusy = false;

        CurrentPackageCreationStage = string.Empty;

        ActiveBackgroungWorker?.Dispose();

        ActiveBackgroungWorker = null;

        if (_abortPackageFlag)
        {
            Model?.UploadQueue?.Clear();

            _abortPackageFlag = false;

            return;
        }

        if (e.Result is Exception ex)
        {
            MessageBox.Show(ex.Message, "Package creation error", MessageBoxButton.OK, MessageBoxImage.Error);

            //todo : Manage generated error
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

        // vpk pack -u MyApp -v 1.0.0 -p path-to/publish/folder -o path-to/releases
        var packDir = Path.GetFullPath(Model.NupkgOutputPath!);
        var outDir = Path.GetFullPath(Model.SquirrelOutputPath!);
        var cmd = $" pack -u {Model.AppId} -v {Model.Version} -p \"{packDir}\" -o \"{outDir}\"";

        // If MainExePath is known (top-level exe), pass it to vpk to avoid auto-detection failures
        if (!string.IsNullOrWhiteSpace(Model.MainExePath) && File.Exists(Model.MainExePath))
        {
            var exeName = Path.GetFileName(Model.MainExePath);
            cmd += $" --mainExe \"{exeName}\"";
        }

        if (File.Exists(Model.IconFilepath))
        {
            // -i is the correct Velopack icon flag
            cmd += " -i \"" + Model.IconFilepath + "\"";
        }

        if (File.Exists(Model.SplashFilepath))
        {
            cmd += " -s \"" + Path.GetFullPath(Model.SplashFilepath) + "\"";
        }

        if (Model.GenerateDeltaPackages == false)
        {
            cmd += " --no-delta";
        }

        if (Model.GenerateMsi && !string.IsNullOrWhiteSpace(Model.MsiBitness))
        {
            cmd += " --msi " + Model.MsiBitness;
        }

        if (!string.IsNullOrWhiteSpace(Model.SignParams))
        {
            cmd += " -n \"" + Model.SignParams + "\"";
        }

        if (!string.IsNullOrWhiteSpace(Model.SignTemplate))
        {
            cmd += " --signTemplate \"" + Model.SignTemplate + "\"";
        }

        var startInfo = new ProcessStartInfo
        {
            WindowStyle = ProcessWindowStyle.Hidden,
            FileName = "vpk",
            Arguments = cmd,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };

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
}
