using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Runtime.Versioning;
using System.Windows;
using System.Windows.Threading;
using Amazon.S3;
using Amazon.S3.Model;
using Amazon.S3.Transfer;
using Amazon.S3.Util;
using CrissCross;
using ReactiveUI;
using ReactiveUI.SourceGenerators;
using Velopack.UI.Models;
using Octokit;

namespace Velopack.UI;

/// <summary>
/// Used in Upload queue list.
/// </summary>
[DataContract]
[SupportedOSPlatform("windows10.0.19041.0")]
public partial class SingleFileUpload : RxObject
{
    private FileUploadStatus _uploadStatus;
    private TransferUtility? _fileTransferUtility;

    /// <summary>
    /// Occurs when [on upload completed].
    /// </summary>
    public event EventHandler<UploadCompleteEventArgs>? OnUploadCompleted;

    /// <summary>
    /// Gets the connection.
    /// </summary>
    /// <value>The connection.</value>
    public WebConnectionBase? Connection { get; internal set; }

    /// <summary>
    /// The absolute target path where the file will be placed (if applicable).
    /// </summary>
    public string? DestinationPath
    {
        get
        {
            if (Connection is FileSystemConnection)
            {
                // Files are already in the Releases folder produced by vpk; FullPath is the destination
                return FullPath;
            }
            if (Connection is AmazonS3Connection s3 && !string.IsNullOrWhiteSpace(s3.BucketName) && !string.IsNullOrWhiteSpace(FullPath))
            {
                return $"s3://{s3.BucketName}/{Path.GetFileName(FullPath)}";
            }
            if (Connection is GitHubReleasesConnection gh && !string.IsNullOrWhiteSpace(gh.Owner) && !string.IsNullOrWhiteSpace(gh.Repository) && !string.IsNullOrWhiteSpace(gh.TagName) && !string.IsNullOrWhiteSpace(FullPath))
            {
                return $"github://{gh.Owner}/{gh.Repository}/releases/{gh.TagName}/{Path.GetFileName(FullPath)}";
            }
            return null;
        }
    }

    /// <summary>
    /// Gets or sets the name of the connection.
    /// </summary>
    /// <value>The name of the connection.</value>
    [DataMember]
    [Reactive]
    public partial string? ConnectionName { get; set; }

    /// <summary>
    /// Gets or sets the filename.
    /// </summary>
    /// <value>The filename.</value>
    [DataMember]
    [Reactive]
    public partial string? Filename { get; set; }

    /// <summary>
    /// Gets or sets the size of the file.
    /// </summary>
    /// <value>The size of the file.</value>
    [DataMember]
    [Reactive]
    public partial string? FileSize { get; set; }

    /// <summary>
    /// Gets the formatted status.
    /// </summary>
    /// <value>The formatted status.</value>
    public string FormattedStatus => UploadStatus.ToString();

    /// <summary>
    /// Gets the full path.
    /// </summary>
    /// <value>The full path.</value>
    public string? FullPath { get; internal set; }

    /// <summary>
    /// Gets or sets the progress percentage.
    /// </summary>
    /// <value>The progress percentage.</value>
    [DataMember]
    [Reactive]
    public partial double ProgressPercentage { get; set; }

    /// <summary>
    /// Gets or sets the upload status.
    /// </summary>
    /// <value>The upload status.</value>
    public FileUploadStatus UploadStatus
    {
        get => _uploadStatus;

        set
        {
            this.RaiseAndSetIfChanged(ref _uploadStatus, value);
            this.RaisePropertyChanged(nameof(FormattedStatus));
        }
    }

    internal async void StartUpload()
    {
        if (Connection is AmazonS3Connection amazonCon)
        {
            if (!CheckInternetConnection.IsConnectedToInternet())
            {
                throw new Exception("Internet Connection not available");
            }

            UploadStatus = FileUploadStatus.InProgress;

            var amazonClient = new AmazonS3Client(amazonCon.AccessKey, amazonCon.SecretAccessKey, amazonCon.GetRegion());

            _fileTransferUtility = new TransferUtility(amazonClient);

            if (!await AmazonS3Util.DoesS3BucketExistV2Async(amazonClient, amazonCon.BucketName))
            {
                await CreateABucketAsync(amazonClient, amazonCon.BucketName);
            }

            var uploadRequest =
                new TransferUtilityUploadRequest
                {
                    BucketName = amazonCon.BucketName,
                    FilePath = FullPath,
                    CannedACL = S3CannedACL.PublicRead,
                    Key = Path.GetFileName(FullPath)
                };

            uploadRequest.UploadProgressEvent += uploadRequest_UploadPartProgressEvent;

            await _fileTransferUtility.UploadAsync(uploadRequest);

            Trace.WriteLine("Start Upload : " + FullPath);
        }
        else if (Connection is FileSystemConnection)
        {
            // For File System destination, vpk already wrote artifacts to the Releases folder.
            // No copy is required; mark as completed so the queue reflects final state.
            UploadStatus = FileUploadStatus.InProgress;
            uploadRequest_UploadPartProgressEvent(this, new UploadProgressArgs(100, 100, 100));
        }
        else if (Connection is GitHubReleasesConnection ghCon)
        {
            if (!CheckInternetConnection.IsConnectedToInternet())
            {
                throw new Exception("Internet Connection not available");
            }

            if (string.IsNullOrWhiteSpace(ghCon.Token) || string.IsNullOrWhiteSpace(ghCon.Owner) || string.IsNullOrWhiteSpace(ghCon.Repository) || string.IsNullOrWhiteSpace(ghCon.TagName) || string.IsNullOrWhiteSpace(FullPath))
            {
                throw new Exception("Missing GitHub configuration");
            }

            UploadStatus = FileUploadStatus.InProgress;
            ProgressPercentage = 5; // coarse update

            var client = new GitHubClient(new ProductHeaderValue("Velopack.UI"))
            {
                Credentials = new Credentials(ghCon.Token)
            };

            // Find or create the release by tag
            Release release;
            try
            {
                release = await client.Repository.Release.Get(ghCon.Owner, ghCon.Repository, ghCon.TagName);
            }
            catch
            {
                var newRelease = new NewRelease(ghCon.TagName)
                {
                    Name = string.IsNullOrWhiteSpace(ghCon.ReleaseName) ? ghCon.TagName : ghCon.ReleaseName,
                    Prerelease = ghCon.Prerelease,
                    Draft = ghCon.Draft,
                };
                release = await client.Repository.Release.Create(ghCon.Owner, ghCon.Repository, newRelease);
            }

            ProgressPercentage = 25;

            // Upload/replace asset
            var fileName = Path.GetFileName(FullPath);

            try
            {
                // If asset exists, delete to replace
                var existing = release.Assets.FirstOrDefault(a => string.Equals(a.Name, fileName, StringComparison.OrdinalIgnoreCase));
                if (existing != null)
                {
                    await client.Repository.Release.DeleteAsset(ghCon.Owner, ghCon.Repository, existing.Id);
                }
            }
            catch { /* ignore */ }

            await using var fs = File.OpenRead(FullPath);
            var upload = new ReleaseAssetUpload
            {
                FileName = fileName,
                ContentType = "application/octet-stream",
                RawData = fs
            };
            _ = await client.Repository.Release.UploadAsset(release, upload);

            ProgressPercentage = 100;
            RequesteUploadComplete(new UploadCompleteEventArgs(this));
        }
    }

    private static async Task CreateABucketAsync(AmazonS3Client client, string? bucketName)
    {
        var putRequest1 = new PutBucketRequest
        {
            BucketName = bucketName,
            UseClientRegion = true
        };
        _ = await client.PutBucketAsync(putRequest1);

        Trace.WriteLine("Creating a bucket " + bucketName);
    }

    private void RequesteUploadComplete(UploadCompleteEventArgs uploadEvent)
    {
        UploadStatus = FileUploadStatus.Completed;
        ProgressPercentage = 100;

        OnUploadCompleted?.Invoke(null, uploadEvent);
    }

    private void uploadRequest_UploadPartProgressEvent(object? sender, UploadProgressArgs e)
    {
        ProgressPercentage = e.PercentDone;

        if (e.PercentDone == 100)
        {
            if (System.Windows.Application.Current.Dispatcher.CheckAccess())
            {
                RequesteUploadComplete(new UploadCompleteEventArgs(this));
            }
            else
            {
                System.Windows.Application.Current.Dispatcher.BeginInvoke(
                  DispatcherPriority.Background,
                  new Action(() => RequesteUploadComplete(new UploadCompleteEventArgs(this))));
            }
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _fileTransferUtility?.Dispose();
        }

        base.Dispose(disposing);
    }
}
