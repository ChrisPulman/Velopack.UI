namespace Velopack.UI;

/// <summary>
/// Upload Complete EventArgs
/// </summary>
/// <seealso cref="EventArgs"/>
/// <remarks>
/// Initializes a new instance of the <see cref="UploadCompleteEventArgs"/> class.
/// </remarks>
/// <param name="sfu">The sfu.</param>
public class UploadCompleteEventArgs(SingleFileUpload sfu) : EventArgs
{
    /// <summary>
    /// Gets the file uploaded.
    /// </summary>
    /// <value>The file uploaded.</value>
    public SingleFileUpload FileUploaded { get; internal set; } = sfu;
}
