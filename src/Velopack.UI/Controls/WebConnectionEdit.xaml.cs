// Copyright (c) Chris Pulman. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Runtime.Versioning;
using System.Windows;

namespace Velopack.UI;

/// <summary>
/// WebConnectionEdit.
/// </summary>
/// <seealso cref="Window" />
/// <seealso cref="System.Windows.Markup.IComponentConnector" />
/// <seealso cref="System.Windows.Markup.IStyleConnector" />
[SupportedOSPlatform("windows10.0.19041.0")]
public partial class WebConnectionEdit
{
    /// <summary>
    /// Initializes a new instance of the <see cref="WebConnectionEdit"/> class.
    /// </summary>
    public WebConnectionEdit() => InitializeComponent();

    private void Button_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = true;
        Close();
    }

    private void Hyperlink_Click(object sender, RoutedEventArgs e) =>
        Process.Start("http://docs.aws.amazon.com/awscloudtrail/latest/userguide/cloudtrail-s3-bucket-naming-requirements.html");
}
