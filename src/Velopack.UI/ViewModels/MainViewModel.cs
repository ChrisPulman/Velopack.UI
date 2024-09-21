using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Reactive;
using System.Text.Json;
using System.Windows;
using CrissCross;
using Microsoft.Win32;
using ReactiveUI;

namespace Velopack.UI;

public class MainViewModel : RxObject
{
    internal BackgroundWorker? ActiveBackgroungWorker;
    private bool _abortPackageFlag;
    private string? _currentPackageCreationStage;
    private bool _isBusy;
    private bool _isSaved;
    private AutoSquirrelModel? _model;
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
    /// Gets or sets the current package creation stage.
    /// </summary>
    /// <value>The current package creation stage.</value>
    public string? CurrentPackageCreationStage
    {
        get => _currentPackageCreationStage;
        set => this.RaiseAndSetIfChanged(ref _currentPackageCreationStage, value);
    }

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
    /// Gets or sets a value indicating whether this instance is busy.
    /// </summary>
    /// <value><c>true</c> if this instance is busy; otherwise, <c>false</c>.</value>
    public bool IsBusy
    {
        get => _isBusy;
        set => this.RaiseAndSetIfChanged(ref _isBusy, value);
    }

    /// <summary>
    /// Gets or sets the model.
    /// </summary>
    /// <value>The model.</value>
    public AutoSquirrelModel? Model
    {
        get => _model;
        set => this.RaiseAndSetIfChanged(ref _model, value);
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
    public void CreateNewProject()
    {
        var rslt = MessageBox.Show("Save current project ?", "New Project", MessageBoxButton.YesNoCancel, MessageBoxImage.Question);

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
    public void OpenProject()
    {
        try
        {
            var ofd = new OpenFileDialog
            {
                AddExtension = true,
                DefaultExt = PathFolderHelper.ProjectFileExtension,
                Filter = PathFolderHelper.FileDialogName
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

            //Save last folder path
        }
        catch (Exception)
        {
            MessageBox.Show("Loading File Error, file no more supported", "Error", MessageBoxButton.OK, MessageBoxImage.Error, MessageBoxResult.None);
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
        catch (Exception)
        {
            MessageBox.Show("Loading File Error, file no more supported", "Error", MessageBoxButton.OK, MessageBoxImage.Error, MessageBoxResult.None);
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

            // I proceed only if i created the project .asproj file and directory I need existing
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
    public void PublishPackageComplete()
    {
        _publishMode = 0;
        PublishPackage();
    }

    /// <summary>
    /// Publishes the package only update.
    /// </summary>
    public void PublishPackageOnlyUpdate()
    {
        _publishMode = 1;
        PublishPackage();
    }

    /// <summary>
    /// Saves this instance.
    /// </summary>
    public void Save()
    {
        if (string.IsNullOrWhiteSpace(FilePath))
        {
            SaveAs();
            return;
        }
        if (FilePath.Contains(".asproj"))
        {
            FilePath = Path.GetDirectoryName(FilePath);
        }

        if (Model == null)
        {
            return;
        }

        Model.NupkgOutputPath = FilePath + Path.DirectorySeparatorChar + Model.AppId + "_files" + PathFolderHelper.PackageDirectory;
        Model.SquirrelOutputPath = FilePath + Path.DirectorySeparatorChar + Model.AppId + "_files" + PathFolderHelper.ReleasesDirectory;

        if (!Directory.Exists(Model.NupkgOutputPath))
        {
            Directory.CreateDirectory(Model.NupkgOutputPath);
        }

        if (!Directory.Exists(Model.SquirrelOutputPath))
        {
            Directory.CreateDirectory(Model.SquirrelOutputPath);
        }

        var asProj = Path.Combine(FilePath!, $"{Model.AppId}.asproj");
        File.WriteAllText(asProj, JsonSerializer.Serialize(Model));
        Trace.WriteLine("FILE SAVED ! : " + FilePath);

        _isSaved = true;

        AddLastProject(asProj);
        this.RaisePropertyChanged(nameof(WindowTitle));
    }

    /// <summary>
    /// Saves as.
    /// </summary>
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
            ActiveBackgroungWorker?.ReportProgress(20, "NUGET PACKAGE CREATING");

            if (ActiveBackgroungWorker?.CancellationPending == true)
            {
                return;
            }

            ActiveBackgroungWorker?.ReportProgress(40, "SQUIRREL PACKAGE CREATING");

            // Releasify
            if (Model?.NupkgOutputPath == null)
            {
                throw new Exception("NupkgOutputPath is null");
            }

            Directory.EnumerateFiles(Model.NupkgOutputPath).ToList().ForEach(File.Delete);
            foreach (var file in Model.PackageFiles)
            {
                if (ActiveBackgroungWorker?.CancellationPending == true)
                {
                    return;
                }

                File.Copy(file.Filename, Model.NupkgOutputPath + Path.DirectorySeparatorChar + Path.GetFileName(file.Filename), true);
            }

            SquirrelReleasify();
            Trace.WriteLine("CREATED SQUIRREL PACKAGE to : " + Model.SquirrelOutputPath);
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

    private void SquirrelReleasify()
    {
        if (Model == null)
        {
               throw new Exception("Model is null");
        }

        /*
        pack: Creates a Squirrel release from a folder containing application files
              -r, --releaseDir=DIRECTORY      Output DIRECTORY for releasified packages
              -u, --packId=ID                 Unique ID for release
              -v, --packVersion=VERSION       Current VERSION for release
              -p, --packDir=DIRECTORY         DIRECTORY containing application files for release
                  --packTitle=NAME            Optional display/friendly NAME for release
                  --packAuthors=AUTHORS       Optional company or list of release AUTHORS
                  --includePdb                Add *.pdb files to release package
                  --releaseNotes=PATH         PATH to file with markdown notes for version
              -n, --signParams=PARAMETERS     Sign files via SignTool.exe using these PARAMETERS
                  --signTemplate=COMMAND      Use a custom signing COMMAND. '{{file}}' will be
                                                replaced by the path of the file to sign.
                  --noDelta                   Skip the generation of delta packages
              -f, --framework=RUNTIMES        List of required RUNTIMES to install during setup
                                                example: 'net6,vcredist143'
              -s, --splashImage=PATH          PATH to image/gif displayed during installation
              -i, --icon=PATH                 PATH to .ico for Setup.exe and Update.exe
                  --appIcon=PATH              PATH to .ico for 'Apps and Features' list
                  --msi=BITNESS               Compile a .msi machine-wide deployment tool with the
                                                specified BITNESS. (either 'x86' or 'x64')

        releasify: Take an existing nuget package and convert it into a Squirrel release
              -r, --releaseDir=DIRECTORY      Output DIRECTORY for releasified packages
              -p, --package=PATH              PATH to a '.nupkg' package to releasify
              -n, --signParams=PARAMETERS     Sign files via SignTool.exe using these PARAMETERS
                  --signTemplate=COMMAND      Use a custom signing COMMAND. '{{file}}' will be
                                                replaced by the path of the file to sign.
                  --noDelta                   Skip the generation of delta packages
              -f, --framework=RUNTIMES        List of required RUNTIMES to install during setup
                                                example: 'net6,vcredist143'
              -s, --splashImage=PATH          PATH to image/gif displayed during installation
              -i, --icon=PATH                 PATH to .ico for Setup.exe and Update.exe
                  --appIcon=PATH              PATH to .ico for 'Apps and Features' list
                  --msi=BITNESS               Compile a .msi machine-wide deployment tool with the
                                                specified BITNESS. (either 'x86' or 'x64')

        Squirrel.exe pack --packId "YourApp" --packVersion "1.0.0" --packDirectory "path-to/publish/folder"
        */
        var cmd = $@" pack -u {Model.AppId} -v {Model.Version} -p {Model.NupkgOutputPath} -r {Model.SquirrelOutputPath}";

        if (File.Exists(Model.IconFilepath))
        {
            cmd += @" -i " + Model.IconFilepath;
            cmd += @" -setupIcon " + Model.IconFilepath;
        }

        if (File.Exists(Model.SplashFilepath))
        {
            cmd += @" -s " + Path.GetFullPath(Model.SplashFilepath);
        }

        var startInfo = new ProcessStartInfo
        {
            WindowStyle = ProcessWindowStyle.Hidden,
            FileName = @"tools\Squirrel.exe",
            Arguments = cmd
        };

        using (_exeProcess = Process.Start(startInfo))
        {
            _exeProcess?.WaitForExit();
        }
    }
}
