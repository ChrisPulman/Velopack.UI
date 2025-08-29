using System.Runtime.Serialization;
using System.Text.Json.Serialization;

namespace Velopack.UI;

/// <summary>
/// Preference
/// </summary>
[DataContract]
public class Preference
{
    /// <summary>
    /// The last opened project
    /// </summary>
    [DataMember]
    [JsonInclude]
    public List<string> LastOpenedProject = [];
}
