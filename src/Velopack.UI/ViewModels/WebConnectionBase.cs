using System;
using System.ComponentModel;
using System.Runtime.Serialization;
using System.Text;
using CrissCross;
using FluentValidation.Results;
using ReactiveUI;

namespace Velopack.UI;

/// <summary>
/// Web Connection Base
/// </summary>
/// <seealso cref="AutoSquirrel.PropertyChangedBaseValidable"/>
public abstract class WebConnectionBase : RxObject, IDataErrorInfo
{
    private string? _connectionName;

    /// <summary>
    /// Gets or sets the name of the connection.
    /// </summary>
    /// <value>The name of the connection.</value>
    [DataMember]
    public string? ConnectionName
    {
        get => _connectionName;
        set => this.RaiseAndSetIfChanged(ref _connectionName, value);
    }

    /// <summary>
    /// Gets an error message indicating what is wrong with this object.
    /// </summary>
    public string Error => GetError(Validate());

    /// <summary>
    /// Returns true if ... is valid.
    /// </summary>
    /// <value><c>true</c> if this instance is valid; otherwise, <c>false</c>.</value>
    public bool IsValid => Validate().IsValid;

    /// <summary>
    /// Gets the <see cref="string"/> with the specified column name.
    /// </summary>
    /// <value>The <see cref="string"/>.</value>
    /// <param name="columnName">Name of the column.</param>
    /// <returns></returns>
    public string this[string columnName]
    {
        get
        {
            var __ValidationResults = Validate();
            if (__ValidationResults == null)
            {
                return string.Empty;
            }

            var __ColumnResults = __ValidationResults.Errors.FirstOrDefault(x => string.Compare(x.PropertyName, columnName, true) == 0);
            return __ColumnResults != null ? __ColumnResults.ErrorMessage : string.Empty;
        }
    }

    /// <summary>
    /// Gets the error.
    /// </summary>
    /// <param name="result">The result.</param>
    /// <returns></returns>
    public static string GetError(ValidationResult result)
    {
        ArgumentNullException.ThrowIfNull(result);

        var __ValidationErrors = new StringBuilder();
        foreach (var validationFailure in result.Errors)
        {
            __ValidationErrors.Append(validationFailure.ErrorMessage);
            __ValidationErrors.Append(Environment.NewLine);
        }

        return __ValidationErrors.ToString();
    }

    /// <summary>
    /// Validates this instance.
    /// </summary>
    /// <returns></returns>
    public virtual ValidationResult Validate() => new();
}
