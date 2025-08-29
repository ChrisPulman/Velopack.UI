using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.Serialization;
using System.Windows;
using System.Windows.Threading;
using Amazon.S3;
using Amazon.S3.Model;
using Amazon.S3.Transfer;
using Amazon.S3.Util;
using CrissCross;
using ReactiveUI;

namespace Velopack.UI;

/// <summary>
/// Used in Upload queue list. I don't need serialization for this class.
/// </summary>
[DataContract]
public class SingleFileUpload : RxObject
{
    private string? _connection;
    private string? _filename;
    private string? _fileSize;
    private double _progressPercentage;
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
            return null;
        }
    }

    /// <summary>
    /// Gets or sets the name of the connection.
    /// </summary>
    /// <value>The name of the connection.</value>
    [DataMember]
    public string? ConnectionName
    {
        get => _connection;
        set => this.RaiseAndSetIfChanged(ref _connection, value);
    }

    /// <summary>
    /// Gets or sets the filename.
    /// </summary>
    /// <value>The filename.</value>
    [DataMember]
    public string? Filename
    {
        get => _filename;

        set => this.RaiseAndSetIfChanged(ref _filename, value);
    }

    /// <summary>
    /// Gets or sets the size of the file.
    /// </summary>
    /// <value>The size of the file.</value>
    [DataMember]
    public string? FileSize
    {
        get => _fileSize;
        set => this.RaiseAndSetIfChanged(ref _fileSize, value);
    }

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
    public double ProgressPercentage
    {
        get => _progressPercentage;
        set => this.RaiseAndSetIfChanged(ref _progressPercentage, value);
    }

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
            if (Application.Current.Dispatcher.CheckAccess())
            {
                RequesteUploadComplete(new UploadCompleteEventArgs(this));
            }
            else
            {
                Application.Current.Dispatcher.BeginInvoke(
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
