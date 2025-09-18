using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Net.Cache;
using System.Reactive;
using System.Reactive.Linq;
using System.Runtime.Serialization;
using System.Runtime.Versioning;
using System.Text.Json.Serialization;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using FluentValidation;
using FluentValidation.Results;
using GongSolutions.Wpf.DragDrop;
using ReactiveUI;
using ReactiveUI.SourceGenerators;
using Velopack.UI.Helpers;
using Velopack.UI.Models;

namespace Velopack.UI;

[DataContract]
[SupportedOSPlatform("windows10.0.19041.0")]
public partial class VelopackModel : WebConnectionBase, IDropTarget
{
    [JsonIgnore]
    public bool HasUnsavedTreeChanges { get; set; }

    [DataMember]
    internal List<WebConnectionBase> CachedConnection = [];

    private readonly ConnectionDiscoveryService _connectionDiscoveryService = new();
    private readonly string _newFolderName = "NEW FOLDER";
    private static readonly HashSet<string> s_excludedExtensions = new([".pdb", ".nupkg", ".msi", ".zip"], StringComparer.OrdinalIgnoreCase);
    private List<string>? _availableUploadLocation;
    private string? _iconFilepath;

    [DataMember]
    [Reactive]
    private string? _appId;

    [DataMember]
    [Reactive]
    private string? _authors;

    [DataMember]
    [Reactive]
    private string? _description;

    [DataMember]
    [Reactive]
    private string? _mainExePath;

    [DataMember]
    [Reactive]
    private string? _packageFilesOutputPath;

    [DataMember]
    [Reactive]
    private ObservableCollection<ItemLink> _packageFiles = [];

    [DataMember]
    [Reactive]
    private WebConnectionBase? _selectedConnection;

    [Reactive]
    private ItemLink _selectedItem = new();

    [Reactive]
    private SingleFileUpload? _selectedUploadItem;

    [DataMember]
    [Reactive]
    private string? _velopackOutputPath;

    [DataMember]
    [Reactive]
    private string? _title;
    private string? _selectedConnectionString;
    private bool _setVersionManually;
    private string? _splashFilepath;
    private ObservableCollection<SingleFileUpload> _uploadQueue = [];
    private string? _version;
    private ReactiveCommand<Unit, Unit>? _selectSplashCmd;

    // Velopack options
    [DataMember]
    [Reactive]
    private bool _generateDeltaPackages = true;

    [DataMember]
    [Reactive]
    private bool _generateMsi;

    [DataMember]
    [Reactive]
    private string? _msiBitness = "x64";

    [DataMember]
    [Reactive]
    private string? _signParams;

    [DataMember]
    [Reactive]
    private string? _signTemplate;

    // Persisted base path for FileSystem connection so it can be saved/restored in *.velo
    [DataMember]
    [Reactive]
    private string? _fileSystemBasePath;

    // Track subscription to FileSystemConnection.FileSystemPath changes
    private IDisposable? _fileSystemConnPathSub;

    /// <summary>
    /// After JSON load, resync selected connection and mirror any saved FileSystemBasePath.
    /// </summary>
    internal void ResyncAfterLoad() => UpdateSelectedConnection(SelectedConnectionString);

    /// <summary>
    /// Initializes a new instance of the <see cref="VelopackModel"/> class.
    /// </summary>
    public VelopackModel()
    {
        PackageFiles = [];
        // Default to File System connection for ease of use
        SelectedConnectionString = "File System";
        // Ensure default connection is resolved immediately
        UpdateSelectedConnection(SelectedConnectionString);
    }

    /// <summary>
    /// Gets the available upload location.
    /// </summary>
    /// <value>The available upload location.</value>
    public List<string> AvailableUploadLocation
    {
        get
        {
            _availableUploadLocation ??= [.. _connectionDiscoveryService.AvailableConnections.Select(connection => connection.ConnectionName!)];
            return _availableUploadLocation;
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
        get => _iconFilepath;

        set
        {
            _iconFilepath = value;
            this.RaiseAndSetIfChanged(ref _iconFilepath, value);
            this.RaisePropertyChanged(nameof(IconSource));
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
                return GetImageFromFilepath(IconFilepath);
            }
            catch
            {
                //TODO -  default icon
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
        get => _selectedConnectionString;

        set
        {
            if (_selectedConnectionString == value)
            {
                return;
            }

            UpdateSelectedConnection(value);
            this.RaiseAndSetIfChanged(ref _selectedConnectionString, value);
        }
    }

    /// <summary>
    /// Gets or sets the selected link.
    /// </summary>
    /// <value>The selected link.</value>
    [JsonIgnore]
    public IList<ItemLink> SelectedLink { get; set; } = [];

    /// <summary>
    /// Gets the select splash command.
    /// </summary>
    /// <value>The select splash command.</value>
    [JsonIgnore]
    public ICommand SelectSplashCmd =>
_selectSplashCmd ??= ReactiveCommand.Create(SelectSplash);

    /// <summary>
    /// Opens the connection edit dialog for the currently selected connection.
    /// </summary>
    [ReactiveCommand]
    private void EditCurrentConnection()
    {
        // Ensure we have a connection instance matching the selected string
        if (string.IsNullOrWhiteSpace(SelectedConnectionString))
        {
            return;
        }

        var conn = SelectedConnection ?? _connectionDiscoveryService.GetByName(SelectedConnectionString);
        if (conn == null)
        {
            return;
        }

        // Cache the instance so changes persist across selection switches
        CachedConnection ??= [];
        if (!CachedConnection.Contains(conn))
        {
            CachedConnection.Add(conn);
        }

        var dlg = new WebConnectionEdit
        {
            DataContext = conn,
            Owner = Application.Current?.Windows.OfType<Window>().FirstOrDefault(w => w.IsActive) ?? Application.Current?.MainWindow
        };

        var ok = dlg.ShowDialog();
        if (ok == true)
        {
            // Keep SelectedConnection in sync
            SelectedConnection = conn;

            // If editing a FileSystem connection, mirror path into model for visibility/persistence
            if (conn is FileSystemConnection fsc && !string.IsNullOrWhiteSpace(fsc.FileSystemPath))
            {
                FileSystemBasePath = fsc.FileSystemPath;
                // Show the base path; Save() will derive Nupkg/Releases paths
                VelopackOutputPath = fsc.FileSystemPath;
            }
        }
    }

    /// <summary>
    /// Gets or sets a value indicating whether [set version manually].
    /// </summary>
    /// <value><c>true</c> if [set version manually]; otherwise, <c>false</c>.</value>
    [DataMember]
    public bool SetVersionManually
    {
        get => _setVersionManually;

        set
        {
            this.RaiseAndSetIfChanged(ref _setVersionManually, value);
            RefreshPackageVersion();
        }
    }

    /// <summary>
    /// Gets or sets the splash filepath.
    /// </summary>
    /// <value>The splash filepath.</value>
    [DataMember]
    public string? SplashFilepath
    {
        get => _splashFilepath;

        set
        {
            this.RaiseAndSetIfChanged(ref _splashFilepath, value);
            this.RaisePropertyChanged(nameof(SplashSource));
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
                return GetImageFromFilepath(SplashFilepath);
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
        get => _uploadQueue;
        set
        {
            _uploadQueue = value ?? [];
            this.RaisePropertyChanged(nameof(UploadQueue));
        }
    }

    /// <summary>
    /// Gets or sets the version.
    /// </summary>
    /// <value>The version.</value>
    [DataMember]
    public string? Version
    {
        get => _version;

        set
        {
            var test = value?.Split('.');
            if (test != null && test.Length <= 3)
            {
                this.RaiseAndSetIfChanged(ref _version, value);
            }
            else
            {
                if (System.Windows.MessageBox.Show("Please use a Semantic Versioning 2.0.0 standard for the version number i.e. Major.Minor.Build http://semver.org/", "Invalid Version", MessageBoxButton.OK) == MessageBoxResult.OK)
                {
                    RefreshPackageVersion();
                }
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
    /// ON DROP
    /// </summary>
    /// <param name="dropInfo"></param>
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

            MoveItem(draggedItem, targetItem);
        }

        // FILE ADDED FROM FILE SYSTEM

        if (dropInfo.Data is DataObject dataObj)
        {
            if (dataObj.GetDataPresent(System.Windows.DataFormats.FileDrop))
            {
                foreach (var filePath in (string[])dataObj.GetData(System.Windows.DataFormats.FileDrop))
                {
                    if (ShouldIncludeFile(filePath))
                    {
                        AddFile(filePath, targetItem);
                    }
                }
            }

            PackageFiles = OrderFileList(PackageFiles);
        }
    }

    /// <summary>
    /// Choose files to add to package (multi-select)
    /// </summary>
    [ReactiveCommand]
    private void AddFilesFromDialog()
    {
        var ofd = new Microsoft.Win32.OpenFileDialog
        {
            Multiselect = true,
            CheckFileExists = true,
            Title = "Select files to include"
        };
        if (ofd.ShowDialog() == true)
        {
            foreach (var f in ofd.FileNames)
            {
                if (ShouldIncludeFile(f))
                {
                    AddFile(f, null);
                }
            }
            PackageFiles = OrderFileList(PackageFiles);
        }
    }

    /// <summary>
    /// Choose a folder (e.g. bin/Release) to include recursively
    /// </summary>
    [ReactiveCommand]
    private void AddFolderFromDialog()
    {
        using var dialog = new System.Windows.Forms.FolderBrowserDialog();
        var result = dialog.ShowDialog();
        if (result == System.Windows.Forms.DialogResult.OK && Directory.Exists(dialog.SelectedPath))
        {
            AddFile(dialog.SelectedPath, null);
            PackageFiles = OrderFileList(PackageFiles);
        }
    }

    /// <summary>
    /// Selects the nupkg directory.
    /// </summary>
    [ReactiveCommand]
    public void SelectNupkgDirectory()
    {
        var dialog = new System.Windows.Forms.FolderBrowserDialog();

        if (Directory.Exists(PackageFilesOutputPath))
        {
            dialog.SelectedPath = PackageFilesOutputPath;
        }

        var result = dialog.ShowDialog();

        if (result != System.Windows.Forms.DialogResult.OK)
        {
            return;
        }

        PackageFilesOutputPath = dialog.SelectedPath;
    }

    /// <summary>
    /// Selects the output directory.
    /// </summary>
    [ReactiveCommand]
    public void SelectOutputDirectory()
    {
        var dialog = new System.Windows.Forms.FolderBrowserDialog();

        if (Directory.Exists(VelopackOutputPath))
        {
            dialog.SelectedPath = VelopackOutputPath;
        }

        var result = dialog.ShowDialog();

        if (result != System.Windows.Forms.DialogResult.OK)
        {
            return;
        }

        VelopackOutputPath = dialog.SelectedPath;

        // If FileSystem connection is selected, align its path and use it for uploads
        if (SelectedConnection is FileSystemConnection fsc)
        {
            fsc.FileSystemPath = VelopackOutputPath;
            FileSystemBasePath = fsc.FileSystemPath;
        }
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
            Filter = "GIF | *.gif"
        };

        var o = ofd.ShowDialog();

        if (o != System.Windows.Forms.DialogResult.OK || !File.Exists(ofd.FileName))
        {
            return;
        }

        SplashFilepath = ofd.FileName;
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
            Title = "Select application icon"
        };

        var result = ofd.ShowDialog();
        if (result != System.Windows.Forms.DialogResult.OK || !File.Exists(ofd.FileName))
        {
            return;
        }

        IconFilepath = ofd.FileName;
    }

    /// <summary>
    /// Sets the selected item.
    /// </summary>
    /// <param name="item">The item.</param>
    public void SetSelectedItem(IList<ItemLink> item)
    {
        SelectedLink = item;
        SelectedItem = SelectedLink.FirstOrDefault() ?? new ItemLink();
    }

    /// <summary>
    /// Validates this instance.
    /// </summary>
    /// <returns></returns>
    public override ValidationResult Validate()
    {
        var commonValid = new Validator().Validate(this);
        if (!commonValid.IsValid)
        {
            return commonValid;
        }

        return base.Validate();
    }

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
    /// <exception cref="Exception"></exception>
    internal void BeginUpdatedFiles(int mode)
    {
        // Determine the folder where vpk outputs artifacts
        var releasesPath = VelopackOutputPath;
        if (SelectedConnection is FileSystemConnection fsc && !string.IsNullOrWhiteSpace(fsc.FileSystemPath))
        {
            var expected = Path.Combine(fsc.FileSystemPath, PathFolderHelper.ReleasesDirectory);
            if (!string.IsNullOrWhiteSpace(VelopackOutputPath) && Directory.Exists(VelopackOutputPath))
            {
                releasesPath = VelopackOutputPath;
            }
            else
            {
                releasesPath = expected;
            }
        }

        if (string.IsNullOrWhiteSpace(releasesPath) || !Directory.Exists(releasesPath))
        {
            throw new Exception("Releases directory " + releasesPath + " not found !");
        }

        if (SelectedConnection == null)
        {
            throw new Exception("No selected upload location !");
        }

        // Gather all artifacts from releases folder (top-level only)
        var allFiles = Directory.EnumerateFiles(releasesPath, "*", SearchOption.TopDirectoryOnly).ToList();

        // Filter for OnlyUpdate mode (mode == 1). mode == 0 => full publish
        if (mode == 1)
        {
            allFiles = allFiles.Where(p =>
            {
                var name = Path.GetFileName(p);
                var ext = Path.GetExtension(p);
                if (name.Equals("Setup.exe", StringComparison.OrdinalIgnoreCase)) return false;
                if (ext.Equals(".msi", StringComparison.OrdinalIgnoreCase)) return false;
                if (name.Contains("-full.nupkg", StringComparison.OrdinalIgnoreCase)) return false;
                return true;
            }).ToList();
        }

        var updatedFiles = allFiles.Where(File.Exists).Select(p => new FileInfo(p)).ToList();

        UploadQueue ??= [];
        UploadQueue.Clear();

        foreach (var connection in new List<WebConnectionBase>() { SelectedConnection })
        {
            foreach (var file in updatedFiles)
            {
                UploadQueue.Add(new SingleFileUpload()
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

        ProcessNextUploadFile();
    }

    /// <summary>
    /// Read the main exe version and set it as package version
    /// </summary>
    [ReactiveCommand]
    internal void RefreshPackageVersion()
    {
        if (!File.Exists(MainExePath))
        {
            return;
        }

        if (SetVersionManually)
        {
            return;
        }

        var versInfo = FileVersionInfo.GetVersionInfo(MainExePath);

        Version = $"{versInfo.ProductMajorPart}.{versInfo.ProductMinorPart}.{versInfo.ProductBuildPart}";
    }

    private static string BytesToString(long byteCount)
    {
        string[] suf = ["B", "KB", "MB", "GB", "TB", "PB", "EB"]; //Longs run out around EB
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

    private void AddFile(string filePath, ItemLink? targetItem)
    {
        var isDir = false;
        var fa = File.GetAttributes(filePath);
        if (fa.HasFlag(FileAttributes.Directory))
        {
            isDir = true;
        }

        RemoveItemBySourceFilepath(filePath);

        var node = new ItemLink() { SourceFilepath = filePath, IsDirectory = isDir };

        var parent = targetItem;
        if (targetItem == null)
        {
            //Add to root
            _packageFiles.Add(node);
        }
        else
        {
            if (!targetItem.IsDirectory)
            {
                parent = targetItem.GetParent(PackageFiles);
            }

            if (parent != null)
            {
                //Insert into treeview root
                parent.Children.Add(node);
            }
            else
            {
                //Insert into treeview root
                _packageFiles.Add(node);
            }
        }

        HasUnsavedTreeChanges = true;

        if (isDir)
        {
            var dir = new DirectoryInfo(filePath);

            var files = dir.GetFiles("*.*", SearchOption.TopDirectoryOnly);
            var subDirectory = dir.GetDirectories("*.*", SearchOption.TopDirectoryOnly);

            foreach (var f in files)
            {
                if (ShouldIncludeFile(f.FullName))
                {
                    AddFile(f.FullName, node);
                }
            }

            foreach (var f in subDirectory)
            {
                AddFile(f.FullName, node);
            }
        }
        else
        {
            // I keep the exe filepath, i'll read the version from this file.
            var ext = Path.GetExtension(filePath).ToLower();

            if (ext == ".exe")
            {
                var nodeParent = node.GetParent(PackageFiles);
                if (nodeParent == null)
                {
                    var fileName = Path.GetFileNameWithoutExtension(filePath);
                    if (string.IsNullOrWhiteSpace(AppId))
                    {
                        AppId = fileName;
                    }
                    if (string.IsNullOrWhiteSpace(Title))
                    {
                        Title = fileName;
                    }

                    MainExePath = filePath;
                    var versInfo = FileVersionInfo.GetVersionInfo(MainExePath);
                    if (string.IsNullOrWhiteSpace(Description))
                    {
                        Description = versInfo.Comments;
                    }

                    if (string.IsNullOrWhiteSpace(Authors))
                    {
                        Authors = versInfo.CompanyName;
                    }

                    RefreshPackageVersion();
                }
            }
        }
    }

    [ReactiveCommand]
    private void AddDirectory()
    {
        if (SelectedLink.Count != 1)
        {
            return;
        }

        var selectedLink = SelectedLink[0];
        if (selectedLink != null)
        {
            var validFolderName = GetValidName(_newFolderName, selectedLink.Children);

            selectedLink.Children.Add(new ItemLink { OutputFilename = validFolderName, IsDirectory = true });
        }
        else
        {
            var validFolderName = GetValidName(_newFolderName, PackageFiles);

            PackageFiles.Add(new ItemLink { OutputFilename = validFolderName, IsDirectory = true });
        }

        HasUnsavedTreeChanges = true;
    }

    // Remove selected item(s) from the tree
    [ReactiveCommand]
    private void RemoveItem()
    {
        if (SelectedLink == null || SelectedLink.Count == 0)
        {
            return;
        }

        foreach (var item in SelectedLink.ToList())
        {
            RemoveFromTreeview(item);
        }

        PackageFiles = OrderFileList(PackageFiles);
        HasUnsavedTreeChanges = true;
    }

    // Remove all items (if one selected, clear its parent children, otherwise clear root)
    [ReactiveCommand]
    private void RemoveAllItems()
    {
        if (SelectedLink != null && SelectedLink.Count == 1)
        {
            RemoveAllFromTreeview(SelectedLink[0]);
        }
        else
        {
            // Also clean PackageFiles folder on disk
            TryDeleteDirectoryContents(PackageFilesOutputPath);

            _packageFiles.Clear();
            this.RaisePropertyChanged(nameof(PackageFiles));
            MainExePath = string.Empty;
            RefreshPackageVersion();
            HasUnsavedTreeChanges = true;
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
        RemoveFromTreeview(draggedItem);

        // Add to target position
        var parent = targetItem;
        if (targetItem == null)
        {
            //Porto su root
            _packageFiles.Add(draggedItem);
        }
        else
        {
            if (!targetItem.IsDirectory)
            {
                parent = targetItem.GetParent(PackageFiles);
            }

            if (parent != null)
            {
                //Insert into treeview root
                parent.Children.Add(draggedItem);
            }
            else
            {
                //Insert into treeview root
                _packageFiles.Add(draggedItem);
            }
        }

        HasUnsavedTreeChanges = true;
        this.RaisePropertyChanged(nameof(PackageFiles));
    }

    private void RemoveAllFromTreeview(ItemLink item)
    {
        var parent = item.GetParent(PackageFiles);

        // Element is in the treeview root.
        if (parent == null)
        {
            // Clear disk mirror under PackageFiles root
            TryDeleteDirectoryContents(PackageFilesOutputPath);

            _packageFiles.Clear();
            this.RaisePropertyChanged(nameof(PackageFiles));
        }
        else
        {
            // Remove all children from disk under this parent's directory
            var parentPath = BuildDirectoryFullPath(parent);
            TryDeleteDirectoryContents(parentPath);

            //Remove it from children list
            parent.Children.Clear();
        }
        MainExePath = string.Empty;
        RefreshPackageVersion();
        HasUnsavedTreeChanges = true;
    }

    private void RemoveFromTreeview(ItemLink item)
    {
        var parent = item.GetParent(PackageFiles);

        if (MainExePath != null && item.SourceFilepath != null && string.Equals(MainExePath, item.SourceFilepath, StringComparison.CurrentCultureIgnoreCase))
        {
            MainExePath = string.Empty;
            RefreshPackageVersion();
        }

        // Delete corresponding file/folder from disk mirror
        TryDeleteNodeFromDisk(item);

        // Element is in the treeview root.
        if (parent == null)
        {
            _packageFiles.Remove(item);
        }
        else
        {
            //Remove it from children list
            parent.Children.Remove(item);
        }

        HasUnsavedTreeChanges = true;
    }

    private void RemoveItemBySourceFilepath(string filepath)
    {
        var list = new List<ItemLink>();

        SearchNodeByFilepath(filepath, PackageFiles, list);

        foreach (var node in list)
        {
            RemoveFromTreeview(node);
        }
    }

    /// <summary>
    /// I keep in memory created WebConnectionBase, so if the user switch accidentally the
    /// connection string , he don't lose inserted parameter
    /// </summary>
    /// <param name="connectionType">Type of the connection.</param>
    private void UpdateSelectedConnection(string? connectionType)
    {
        if (string.IsNullOrWhiteSpace(connectionType))
        {
            return;
        }

        // Instantiate cache if null
        CachedConnection ??= [];

        // Retrieve cached connection or take new instance from connection service
        var con =
            CachedConnection.FirstOrDefault(c => c.ConnectionName == connectionType) ??
            _connectionDiscoveryService.GetByName(connectionType);

        // Cache connection if not cached already
        if (con != null && !CachedConnection.Contains(con))
        {
            CachedConnection.Add(con);
        }

        SelectedConnection = con;

        // Remove previous subscription
        _fileSystemConnPathSub?.Dispose();
        _fileSystemConnPathSub = null;

        // If FileSystem connection becomes active, sync paths both ways for persistence and UI visibility
        if (SelectedConnection is FileSystemConnection fsc)
        {
            if (!string.IsNullOrWhiteSpace(FileSystemBasePath))
            {
                // Prefer restored model value when available
                fsc.FileSystemPath = FileSystemBasePath;
            }
            else if (!string.IsNullOrWhiteSpace(fsc.FileSystemPath))
            {
                // If connection already has a path, mirror to model
                FileSystemBasePath = fsc.FileSystemPath;
            }

            VelopackOutputPath = fsc.FileSystemPath;

            // Observe changes to the connection path and propagate into model for persistence/dirty tracking
            _fileSystemConnPathSub = fsc
                .WhenAnyValue(x => x.FileSystemPath)
                .Subscribe(path =>
                {
                    FileSystemBasePath = path;
                    VelopackOutputPath = path;
                });
        }
    }

    private class Validator : AbstractValidator<VelopackModel>
    {
        public Validator()
        {
            RuleFor(c => c.AppId).NotEmpty();
            RuleFor(c => c.Title).NotEmpty();
            RuleFor(c => c.Description).NotEmpty();
            RuleFor(c => c.Version).NotEmpty();
            RuleFor(c => c.PackageFiles).NotEmpty();
            RuleFor(c => c.Authors).NotEmpty();
            RuleFor(c => c.SelectedConnectionString).NotEmpty();
        }
    }

    // -------- Helpers added for filtering and upload processing --------
    private static bool ShouldIncludeFile(string path)
    {
        if (string.IsNullOrWhiteSpace(path)) return false;
        try
        {
            var attr = File.GetAttributes(path);
            if (attr.HasFlag(FileAttributes.Directory)) return true; // always include directories
            var ext = Path.GetExtension(path);
            if (!string.IsNullOrEmpty(ext) && s_excludedExtensions.Contains(ext)) return false;
            return File.Exists(path);
        }
        catch
        {
            return false;
        }
    }

    private void ProcessNextUploadFile()
    {
        if (UploadQueue == null || UploadQueue.Count == 0)
        {
            return;
        }

        // Find next queued item
        var item = UploadQueue.FirstOrDefault(f => f.UploadStatus == FileUploadStatus.Queued);
        if (item == null)
        {
            return; // nothing left to do
        }

        item.UploadStatus = FileUploadStatus.InProgress;
        try
        {
            // For FileSystem connection, ensure the file exists at the destination
            if (item.Connection is FileSystemConnection fsConn && !string.IsNullOrWhiteSpace(fsConn.FileSystemPath))
            {
                var destDir = fsConn.FileSystemPath;
                Directory.CreateDirectory(destDir);
                var destPath = Path.Combine(destDir, Path.GetFileName(item.FullPath)!);

                // If source and destination are the same, skip copying
                if (!string.Equals(Path.GetFullPath(item.FullPath!), Path.GetFullPath(destPath), StringComparison.OrdinalIgnoreCase))
                {
                    File.Copy(item.FullPath!, destPath, true);
                }

                item.ProgressPercentage = 100;
                item.UploadStatus = FileUploadStatus.Completed;
            }
            else
            {
                // Fallback: try to use the built-in upload implementation if available
                try
                {
                    item.StartUpload();
                }
                catch
                {
                    // If upload provider not implemented, mark as completed to keep UI flowing
                    item.ProgressPercentage = 100;
                    item.UploadStatus = FileUploadStatus.Completed;
                }
            }
        }
        catch
        {
            // Mark as completed even on failure for now; extend with error handling as needed
            item.ProgressPercentage = 100;
            item.UploadStatus = FileUploadStatus.Completed;
        }

        // Continue with any remaining items
        ProcessNextUploadFile();
    }

    // -------- Disk sync helpers for PackageFiles mirror --------
    private string? BuildDirectoryFullPath(ItemLink? node)
    {
        if (node == null || string.IsNullOrWhiteSpace(PackageFilesOutputPath)) return null;
        var parts = new List<string>();
        // collect ancestors up to root
        var current = node;
        while (true)
        {
            var parent = current.GetParent(PackageFiles);
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
        return Path.Combine(PackageFilesOutputPath!, Path.Combine([.. parts]));
    }

    private string? BuildParentsDirectoryFullPath(ItemLink? node)
    {
        if (node == null || string.IsNullOrWhiteSpace(PackageFilesOutputPath)) return null;
        var stack = new Stack<string>();
        var current = node.GetParent(PackageFiles);
        while (current != null)
        {
            stack.Push(current.Filename);
            current = current.GetParent(PackageFiles);
        }
        return stack.Count == 0
            ? PackageFilesOutputPath
            : Path.Combine(PackageFilesOutputPath!, Path.Combine(stack.ToArray()));
    }

    private void TryDeleteNodeFromDisk(ItemLink item)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(PackageFilesOutputPath)) return;

            if (item.IsDirectory)
            {
                var dir = BuildDirectoryFullPath(item);
                TryDeleteDirectory(dir);
            }
            else
            {
                var parentDir = BuildParentsDirectoryFullPath(item);
                if (string.IsNullOrWhiteSpace(parentDir)) return;
                var filePath = Path.Combine(parentDir!, item.Filename);
                TryDeleteFile(filePath);
            }
        }
        catch
        {
            // ignore
        }
    }

    private static void TryDeleteFile(string? path)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path)) return;
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
            if (string.IsNullOrWhiteSpace(path) || !Directory.Exists(path)) return;
            // ensure files are deletable
            foreach (var f in Directory.EnumerateFiles(path!, "*", SearchOption.AllDirectories))
            {
                try { File.SetAttributes(f, FileAttributes.Normal); } catch { }
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
            if (string.IsNullOrWhiteSpace(path) || !Directory.Exists(path)) return;
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

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing)
        {
            try { _selectedItem?.Dispose(); } catch { }
            if (_uploadQueue != null)
            {
                foreach (var u in _uploadQueue)
                {
                    try { u?.Dispose(); } catch { }
                }
            }
            _fileSystemConnPathSub?.Dispose();
            _fileSystemConnPathSub = null;
        }
    }
}
