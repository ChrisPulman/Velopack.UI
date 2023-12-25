using System;
using System.Runtime.Serialization;
using CrissCross;
using ReactiveUI;

namespace Clowd.Squirrel.UI;

/// <summary>
/// Web Connection Base
/// </summary>
/// <seealso cref="AutoSquirrel.PropertyChangedBaseValidable"/>
public abstract class WebConnectionBase : RxObject
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
}
