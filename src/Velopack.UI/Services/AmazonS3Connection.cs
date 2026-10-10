// <copyright file="AmazonS3Connection.cs" company="Chris Pulman">
// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.
// </copyright>

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
    private string? accessKey;
    private List<string>? availableRegionList;

    /// <summary>
    /// http://docs.aws.amazon.com/awscloudtrail/latest/userguide/cloudtrail-s3-bucket-naming-requirements.html.
    /// </summary>
    private string? bucketName;

    private string? regionName;

    [DataMember]
    [Reactive]
    private string? secretAccessKey;

    /// <summary>
    /// Initializes a new instance of the <see cref="AmazonS3Connection"/> class.
    /// </summary>
    public AmazonS3Connection() => this.ConnectionName = "Amazon S3";

    /// <summary>
    /// Gets the available region list.
    /// </summary>
    /// <value>The available region list.</value>
    public List<string> AvailableRegionList
    {
        get
        {
            if (this.availableRegionList == null)
            {
                this.availableRegionList = [];

                foreach (var r in RegionEndpoint.EnumerableAllRegions)
                {
                    this.availableRegionList.Add(r.DisplayName);
                }
            }

            return this.availableRegionList;
        }
    }

    /// <summary>
    /// Gets or sets the name of the bucket.
    /// </summary>
    /// <value>The name of the bucket.</value>
    [DataMember]
    public string? BucketName
    {
        get => this.bucketName;

        set
        {
            this.bucketName = value;
            if (this.bucketName != null)
            {
                this.bucketName = this.bucketName.ToLower().Replace(" ", string.Empty);
            }

            this.RaisePropertyChanged(nameof(this.BucketName));
            this.RaisePropertyChanged(nameof(this.SetupDownloadUrl));
        }
    }

    /// <summary>
    /// Gets or sets the name of the region.
    /// </summary>
    /// <value>The name of the region.</value>
    [DataMember]
    public string? RegionName
    {
        get => this.regionName;

        set
        {
            this.RaiseAndSetIfChanged(ref this.regionName, value);
            this.RaisePropertyChanged(nameof(this.SetupDownloadUrl));
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
            if (string.IsNullOrWhiteSpace(this.BucketName) || string.IsNullOrWhiteSpace(this.RegionName))
            {
                return "Missing Parameter";
            }

            return "https://s3-" + this.GetRegion()?.SystemName + ".amazonaws.com/" + this.BucketName.ToLower() + "/Setup.exe";
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

    /// <summary>Resolves the configured display name to an Amazon region.</summary>
    /// <returns>The matching region, or null when the configuration is invalid.</returns>
    internal RegionEndpoint? GetRegion() =>
        RegionEndpoint.EnumerableAllRegions.FirstOrDefault(r => r.DisplayName == this.RegionName);

    private class Validator : AbstractValidator<AmazonS3Connection>
    {
        public Validator()
        {
            // RuleFor(c => c.ConnectionName).NotEmpty();
            this.RuleFor(c => c.RegionName).NotEmpty();
            this.RuleFor(c => c.SecretAccessKey).NotEmpty();
            this.RuleFor(c => c.AccessKey).NotEmpty();
            this.RuleFor(c => c.BucketName).Must(CheckBucketName).WithState(x => "Bucket Name not valid ! See Amazon SDK documentation");
        }

        private static bool CheckBucketName(string? bucketName) => !string.IsNullOrWhiteSpace(bucketName) && !bucketName.Contains(' ');
    }
}
