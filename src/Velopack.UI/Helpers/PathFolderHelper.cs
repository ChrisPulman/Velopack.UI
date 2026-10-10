// <copyright file="PathFolderHelper.cs" company="Chris Pulman">
// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.
// </copyright>

using System.IO;
using System.Reflection;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Windows;

namespace Velopack.UI.Helpers;

/// <summary>
/// Path Folder Helper.
/// </summary>
internal static class PathFolderHelper
{
    /// <summary>
    /// The program name.
    /// </summary>
    public const string ProgramName = "Velopack.UI";

    /// <summary>
    /// The project file extension.
    /// </summary>
    public const string ProjectFileExtension = ".velo";

    /// <summary>
    /// The program base directory.
    /// </summary>
    public const string ProgramBaseDirectory = "\\" + ProgramName;

    /// <summary>
    /// The file dialog name.
    /// </summary>
    public const string FileDialogName = ProgramName + " | *" + ProjectFileExtension;

    /// <summary>
    /// Use directory names without leading/trailing separators.
    /// </summary>
    internal const string PackageFilesDirectory = "PackageFiles";

    /// <summary>Names the directory containing release artifacts.</summary>
    internal const string ReleasesDirectory = "Releases";
    private const string ProjectDirectory = "\\Projects\\";

    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    /// <summary>
    /// Gets the correct filepath.
    /// </summary>
    /// <param name="path">The path.</param>
    /// <param name="filename">The filename.</param>
    /// <param name="fileExt">The file ext.</param>
    /// <returns>A path that does not overwrite an existing file.</returns>
    public static string GetCorrectFilepath(string path, string filename, string fileExt)
    {
        var filePath = $"{path}\\{filename}.{fileExt}";

        for (var fileC = 0; File.Exists(filePath); fileC++)
        {
            filePath = $"{path}\\{filename}_{fileC}.{fileExt}";
        }

        return filePath;
    }

    /// <summary>
    /// Gets my directory.
    /// </summary>
    /// <param name="directory">The directory.</param>
    /// <returns>The application directory, creating it when necessary.</returns>
    public static string GetMyDirectory(MyDirectory directory)
    {
        var folderPath = string.Empty;

        switch (directory)
        {
            case MyDirectory.Project:
                {
                    folderPath = GetMyDirectory(MyDirectory.Base) + ProjectDirectory;
                    break;
                }

            case MyDirectory.Base:
                {
                    folderPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments) + ProgramBaseDirectory;
                    break;
                }
        }

        if (string.IsNullOrWhiteSpace(folderPath))
        {
            throw new ArgumentOutOfRangeException(nameof(directory), directory, "Unsupported application directory.");
        }

        if (!Directory.Exists(folderPath))
        {
            Directory.CreateDirectory(folderPath);
        }

        return folderPath;
    }

    /// <summary>Returns the running assembly's major, minor, and build version.</summary>
    /// <returns>The display version for the running application.</returns>
    internal static string GetProgramVersion()
    {
        var ver = Assembly.GetExecutingAssembly()
                         .GetName()
                         .Version;

        return $"{ver?.Major}.{ver?.Minor}.{ver?.Build}";
    }

    /// <summary>Loads saved preferences, removing project paths that no longer exist.</summary>
    /// <returns>The saved preferences, or defaults when the file cannot be loaded.</returns>
    internal static Preference LoadUserPreference()
    {
        try
        {
            var path = GetMyDirectory(MyDirectory.Base) + "\\Preference.txt";

            if (File.Exists(path))
            {
                // Deserialize using System.Text.Json using the path
                var jsonString = File.ReadAllText(path);
                var p = JsonSerializer.Deserialize<Preference>(jsonString) ?? new Preference();

                var temp = p.LastOpenedProject.ToList();

                p.LastOpenedProject.Clear();

                foreach (var fp in temp)
                {
                    if (File.Exists(fp))
                    {
                        p.LastOpenedProject.Add(fp);
                    }
                }

                return p;
            }

            return new Preference();
        }
        catch (Exception)
        {
            return new Preference();
        }
    }

    /// <summary>Saves preferences to the current user's application directory.</summary>
    /// <param name="userPreference">The preferences to persist.</param>
    internal static void SavePreference(Preference userPreference)
    {
        try
        {
            var path = GetMyDirectory(MyDirectory.Base) + "\\Preference.txt";
            var jsonString = JsonSerializer.Serialize(userPreference, JsonOptions);
            File.WriteAllText(path, jsonString);
        }
        catch (Exception)
        {
            MessageBox.Show("Error on saving preference !");
        }
    }

    /// <summary>Removes characters that Windows does not allow in file names.</summary>
    /// <param name="filename">The proposed file name.</param>
    /// <returns>The sanitized file name.</returns>
    internal static string ValidateFilename(string filename)
    {
        var invalidChars = Regex.Escape(new string(Path.GetInvalidFileNameChars()));
        var invalidReStr = $@"([{invalidChars}]*\.+$)|([{invalidChars}]+)";
        return Regex.Replace(filename, invalidReStr, string.Empty);
    }
}
