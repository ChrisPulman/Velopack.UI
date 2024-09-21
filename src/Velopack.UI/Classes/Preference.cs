using System.Runtime.Serialization;

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
    public List<string> LastOpenedProject = new();
}
