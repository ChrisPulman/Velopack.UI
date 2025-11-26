// Copyright (c) Chris Pulman. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Runtime.Serialization;
using System.Text.Json.Serialization;

namespace Velopack.UI;

/// <summary>
/// Preference.
/// </summary>
[DataContract]
public class Preference
{
    /// <summary>
    /// Gets the last opened project.
    /// </summary>
    [DataMember]
    [JsonInclude]
    public List<string> LastOpenedProject { get; } = [];
}
