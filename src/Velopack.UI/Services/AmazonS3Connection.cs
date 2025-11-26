// Copyright (c) Chris Pulman. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Runtime.Serialization;
using System.Runtime.Versioning;
using Amazon;
using FluentValidation;
using FluentValidation.Results;
using ReactiveUI;
using ReactiveUI.SourceGenerators;

namespace Velopack.UI;

/// <summary>
/// This class contains all information about WebConncetion uploading. Information for user :
/// Credentials are stored in clear format.
/// </summary>
[DataContract]
[SupportedOSPlatform("windows10.0.19041.0")]
public partial class AmazonS3Connection : WebConnectionBase
{
    [DataMember]
    [Reactive]
    private string? _accessKey;
    private List<string>? _availableRegionList;

    // http://docs.aws.amazon.com/awscloudtrail/latest/userguide/cloudtrail-s3-bucket-naming-requirements.html
    private string? _bucketName;

    private string? _regionName;

    [DataMember]
    [Reactive]
    private string? _secretAccessKey;

    /// <summary>
    /// Initializes a new instance of the <see cref="AmazonS3Connection"/> class.
    /// </summary>
    public AmazonS3Connection() => ConnectionName = "Amazon S3";

    /// <summary>
    /// Gets the available region list.
    /// </summary>
    /// <value>The available region list.</value>
    public List<string> AvailableRegionList
    {
        get
        {
            if (_availableRegionList == null)
            {
                _availableRegionList = [];

                foreach (var r in RegionEndpoint.EnumerableAllRegions)
                {
                    _availableRegionList.Add(r.DisplayName);
                }
            }

            return _availableRegionList;
        }
    }

    /// <summary>
    /// Gets or sets the name of the bucket.
    /// </summary>
    /// <value>The name of the bucket.</value>
    [DataMember]
    public string? BucketName
    {
        get => _bucketName;

        set
        {
            _bucketName = value;
            if (_bucketName != null)
            {
                _bucketName = _bucketName.ToLower().Replace(" ", string.Empty);
            }

            this.RaisePropertyChanged(nameof(BucketName));
            this.RaisePropertyChanged(nameof(SetupDownloadUrl));
        }
    }

    /// <summary>
    /// Gets or sets the name of the region.
    /// </summary>
    /// <value>The name of the region.</value>
    [DataMember]
    public string? RegionName
    {
        get => _regionName;

        set
        {
            this.RaiseAndSetIfChanged(ref _regionName, value);
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
            if (string.IsNullOrWhiteSpace(BucketName) || string.IsNullOrWhiteSpace(RegionName))
            {
                return "Missing Parameter";
            }

            return "https://s3-" + GetRegion()?.SystemName + ".amazonaws.com/" + BucketName.ToLower() + "/Setup.exe";
        }
    }

    /// <summary>
    /// Validates this instance.
    /// </summary>
    /// <returns>A ValidationResult.</returns>
    public override ValidationResult Validate()
    {
        var commonValid = new Validator().Validate(this);
        if (!commonValid.IsValid)
        {
            return commonValid;
        }

        return base.Validate();
    }

    internal RegionEndpoint? GetRegion() =>
        RegionEndpoint.EnumerableAllRegions.FirstOrDefault(r => r.DisplayName == RegionName);

    private class Validator : AbstractValidator<AmazonS3Connection>
    {
        public Validator()
        {
            // RuleFor(c => c.ConnectionName).NotEmpty();
            RuleFor(c => c.RegionName).NotEmpty();
            RuleFor(c => c.SecretAccessKey).NotEmpty();
            RuleFor(c => c.AccessKey).NotEmpty();
            RuleFor(c => c.BucketName).Must(CheckBucketName).WithState(x => "Bucket Name not valid ! See Amazon SDK documentation");
        }

        private static bool CheckBucketName(string? bucketName) => !string.IsNullOrWhiteSpace(bucketName) && !bucketName.Contains(' ');
    }
}
