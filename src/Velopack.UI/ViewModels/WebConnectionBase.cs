// Copyright (c) Chris Pulman. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.ComponentModel;
using System.Runtime.Serialization;
using System.Runtime.Versioning;
using System.Text;
using System.Text.Json.Serialization;
using CrissCross;
using FluentValidation.Results;
using ReactiveUI.SourceGenerators;

namespace Velopack.UI;

/// <summary>
/// Web Connection Base.
/// </summary>
[DataContract]
[SupportedOSPlatform("windows10.0.19041.0")]
[JsonDerivedType(typeof(GitHubReleasesConnection), nameof(GitHubReleasesConnection))]
[JsonDerivedType(typeof(AmazonS3Connection), nameof(AmazonS3Connection))]
[JsonDerivedType(typeof(FileSystemConnection), nameof(FileSystemConnection))]
public abstract partial class WebConnectionBase : RxObject, IDataErrorInfo
{
    [DataMember]
    [Reactive]
    private string? _connectionName;

    /// <summary>
    /// Gets an error message indicating what is wrong with this object.
    /// </summary>
    public string Error => GetError(Validate());

    /// <summary>
    /// Gets a value indicating whether returns true if ... is valid.
    /// </summary>
    /// <value><c>true</c> if this instance is valid; otherwise, <c>false</c>.</value>
    public bool IsValid => Validate().IsValid;

    /// <summary>
    /// Gets the <see cref="string"/> with the specified column name.
    /// </summary>
    /// <value>The <see cref="string"/>.</value>
    /// <param name="columnName">Name of the column.</param>
    /// <returns>A string.</returns>
    public string this[string columnName]
    {
        get
        {
            var validationResults = Validate();
            if (validationResults == null)
            {
                return string.Empty;
            }

            var columnResults = validationResults.Errors.FirstOrDefault(x => string.Compare(x.PropertyName, columnName, true) == 0);
            return columnResults != null ? columnResults.ErrorMessage : string.Empty;
        }
    }

    /// <summary>
    /// Gets the error.
    /// </summary>
    /// <param name="result">The result.</param>
    /// <returns>A string.</returns>
    public static string GetError(ValidationResult result)
    {
        ArgumentNullException.ThrowIfNull(result);

        var validationErrors = new StringBuilder();
        foreach (var validationFailure in result.Errors)
        {
            validationErrors.Append(validationFailure.ErrorMessage)
                .Append(Environment.NewLine);
        }

        return validationErrors.ToString();
    }

    /// <summary>
    /// Validates this instance.
    /// </summary>
    /// <returns>A ValidationResult.</returns>
    public virtual ValidationResult Validate() => new();
}
