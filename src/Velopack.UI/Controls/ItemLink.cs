using System.Collections.ObjectModel;
using System.Drawing;
using System.IO;
using System.Runtime.Serialization;
using System.Runtime.Versioning;
using System.Text.Json.Serialization;
using System.Windows.Media;
using ReactiveUI;
using ReactiveUI.SourceGenerators;
using static Velopack.UI.IconHelper;

namespace Velopack.UI;

[SupportedOSPlatform("windows10.0.19041.0")]
public partial class ItemLink : CrissCross.RxObject
{
    private static readonly ItemLink s_dummyChild = new();

    [DataMember]
    private ObservableCollection<ItemLink> _children = [];

    [DataMember]
    [Reactive]
    private bool _isSelected;
    private string? _sourceFilepath;
    private string? _filename;

    /// <summary>
    /// Initializes a new instance of the <see cref="ItemLink"/> class.
    /// </summary>
    public ItemLink()
    {
    }

    /// <summary>
    /// Returns the logical child items of this object.
    /// </summary>
    public ObservableCollection<ItemLink> Children
    {
        get => _children;
        set => this.RaiseAndSetIfChanged(ref _children, value);
    }

    /// <summary>
    /// Gets the file dimension.
    /// </summary>
    /// <value>The file dimension.</value>
    [DataMember]
    public double FileDimension { get; internal set; }

    /// <summary>
    /// Gets the file icon.
    /// </summary>
    /// <value>The file icon.</value>
    [JsonIgnore]
    public ImageSource? FileIcon
    {
        get
        {
            try
            {
                Icon? icon = null;

                if (IsDirectory && IsExpanded)
                {
                    icon = GetFolderIcon(IconSize.Large, FolderType.Open);
                }
                else if (IsDirectory && !IsExpanded)
                {
                    icon = GetFolderIcon(IconSize.Large, FolderType.Closed);
                }
                else
                {
                    if (File.Exists(SourceFilepath))
                    {
                        icon = Icon.ExtractAssociatedIcon(SourceFilepath);
                    }
                    else
                    {
                        return FindIconForFilename(Path.GetFileName(SourceFilepath), true);
                    }
                }
                if (icon == null)
                {
                    return null;
                }

                return icon.ToImageSource();
            }
            catch
            {
                //TODO - Get default icon
                return null;
            }
        }
    }

    /// <summary>
    /// Gets or sets the filename.
    /// </summary>
    /// <value>The filename.</value>
    [DataMember]
    public string Filename
    {
        get
        {
            if (!string.IsNullOrWhiteSpace(OutputFilename))
            {
                return OutputFilename;
            }

            if (!string.IsNullOrWhiteSpace(SourceFilepath))
            {
                return Path.GetFileName(SourceFilepath);
            }

            return "no_namefile";
        }

        set
        {
            OutputFilename = value;
            this.RaiseAndSetIfChanged(ref _filename, value);
        }
    }

    /// <summary>
    /// Returns true if this object's Children have not yet been populated.
    /// </summary>
    [JsonIgnore]
    public bool HasDummyChild => Children.Count == 1 && Children[0] == s_dummyChild;

    /// <summary>
    /// Gets or sets a value indicating whether this instance is directory.
    /// </summary>
    /// <value><c>true</c> if this instance is directory; otherwise, <c>false</c>.</value>
    [DataMember]
    public bool IsDirectory { get; set; }

    /// <summary>
    /// Gets/sets whether the TreeViewItem associated with this object is expanded.
    /// </summary>
    [DataMember]
    public bool IsExpanded
    {
        get => _isExpanded;

        set
        {
            if (value != _isExpanded)
            {
                _isExpanded = value;
                this.RaisePropertyChanged(nameof(IsExpanded));
                this.RaisePropertyChanged(nameof(FileIcon));
            }

            // Lazy load the child items, if necessary.
            if (HasDummyChild)
            {
                Children.Remove(s_dummyChild);
                LoadChildren();
            }
        }
    }

    /// <summary>
    /// Fixed folder. Can't remove or move.
    /// </summary>
    [DataMember]
    public bool IsRootBase { get; internal set; }

    /// <summary>
    /// Gets the last edit.
    /// </summary>
    /// <value>The last edit.</value>
    [DataMember]
    public string? LastEdit { get; internal set; }

    /// <summary>
    /// Gets the output filename.
    /// </summary>
    /// <value>The output filename.</value>
    [DataMember]
    public string? OutputFilename { get; internal set; }

    /// <summary>
    /// Filepath of linked source file. Absolute ?
    /// </summary>
    [DataMember]
    public string? SourceFilepath
    {
        get => _sourceFilepath;

        set
        {
            _sourceFilepath = value;
            this.RaisePropertyChanged(nameof(SourceFilepath));
            if (string.IsNullOrWhiteSpace(value))
            {
                return;
            }

            this.RaisePropertyChanged(nameof(Filename));
            try
            {
                var fa = File.GetAttributes(value);
                if ((fa & FileAttributes.Directory) != 0)
                {
                    SetDirectoryInfo(value);
                    return;
                }

                var fileInfo = new FileInfo(value);
                LastEdit = fileInfo.LastWriteTime.ToString();
                FileDimension = fileInfo.Length;
            }
            catch
            {
            }
        }
    }

    private bool _isExpanded { get; set; }

    /// <summary>
    /// Gets the parent.
    /// </summary>
    /// <param name="root">The root.</param>
    /// <returns></returns>
    public ItemLink GetParent(ObservableCollection<ItemLink> root)
    {
        ArgumentNullException.ThrowIfNull(root);

        foreach (var node in root)
        {
            var p = FindParent(this, node);
            if (p != null)
            {
                return p;
            }
        }

        return default!;
    }

    /// <summary>
    /// Invoked when the child items need to be loaded on demand. Subclasses can override this to
    /// populate the Children collection.
    /// </summary>
    protected virtual void LoadChildren()
    {
    }

    private static ItemLink FindParent(ItemLink link, ItemLink node)
    {
        if (node.Children != null)
        {
            if (node.Children.Contains(link))
            {
                return node;
            }

            foreach (var child in node.Children)
            {
                var p = FindParent(link, child);
                if (p != null)
                {
                    return p;
                }
            }
        }

        return default!;
    }

    private static string? GetDirectoryName(string relativeOutputPath)
    {
        var directories = relativeOutputPath.Split(new List<char> { Path.DirectorySeparatorChar }.ToArray(), StringSplitOptions.RemoveEmptyEntries);

        return directories.LastOrDefault();
    }

    private void SetDirectoryInfo(string folderPath)
    {
        var dirInfo = new DirectoryInfo(folderPath);
        LastEdit = dirInfo.LastWriteTime.ToString();
        FileDimension = dirInfo.EnumerateFiles("*.*", SearchOption.AllDirectories).Sum(fi => fi.Length);
    }
}
