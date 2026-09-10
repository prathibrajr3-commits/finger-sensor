using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.IO.Compression;
using System.Text;
using AirGestureAI.Utilities;

namespace AirGestureAI.ApiDocs
{
    /// <summary>
    /// Generates ready-to-compile C# plugin sample projects for developers.
    /// Produces a ZIP archive containing a solution, project file, and minimal source code.
    /// </summary>
    public sealed class SampleProjectGenerator
    {
        private readonly string _outputDirectory;

        /// <summary>
        /// Initializes a new <see cref="SampleProjectGenerator"/>.
        /// </summary>
        /// <param name="outputDirectory">Directory where generated ZIP archives are saved.</param>
        public SampleProjectGenerator(string outputDirectory)
        {
            _outputDirectory = outputDirectory ?? throw new ArgumentNullException(nameof(outputDirectory));
            Directory.CreateDirectory(outputDirectory);
        }

        /// <summary>
        /// Generates a sample plugin project ZIP for the given plugin name.
        /// </summary>
        /// <param name="pluginName">Name for the generated project (used as class and namespace).</param>
        /// <returns>Absolute path to the generated ZIP file.</returns>
        public string Generate(string pluginName)
        {
            if (string.IsNullOrWhiteSpace(pluginName))
                throw new ArgumentException("Plugin name must not be empty.", nameof(pluginName));

            var safe    = pluginName.Replace(" ", "");
            var zipPath = Path.Combine(_outputDirectory, $"{safe}.zip");

            using var archive = ZipFile.Open(zipPath, ZipArchiveMode.Create);
            AddEntry(archive, $"{safe}.sln",      SlnContent(safe));
            AddEntry(archive, $"{safe}/{safe}.csproj", CsprojContent(safe));
            AddEntry(archive, $"{safe}/Plugin.cs",     PluginContent(safe));
            AddEntry(archive, "README.md",             ReadmeContent(safe));

            Logger.Info($"SampleProjectGenerator: Created sample project → '{zipPath}'");
            return zipPath;
        }

        private static string SlnContent(string name) =>
            $"Microsoft Visual Studio Solution File, Format Version 12.00\n" +
            $"Project(\"{{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}}\") = \"{name}\", " +
            $"\"{name}\\{name}.csproj\", \"{{AAAAAAAA-0000-0000-0000-000000000001}}\"\n" +
            $"EndProject\n";

        private static string CsprojContent(string name) =>
            $"<Project Sdk=\"Microsoft.NET.Sdk\">\n" +
            $"  <PropertyGroup>\n" +
            $"    <TargetFramework>net8.0-windows</TargetFramework>\n" +
            $"    <AssemblyName>{name}</AssemblyName>\n" +
            $"    <RootNamespace>{name}</RootNamespace>\n" +
            $"  </PropertyGroup>\n" +
            $"</Project>\n";

        private static string PluginContent(string name) =>
            $"namespace {name}\n{{\n" +
            $"    public sealed class {name}Plugin\n    {{\n" +
            $"        public string Name => \"{name}\";\n" +
            $"        public bool Initialize() => true;\n" +
            $"        public void Start() {{ }}\n" +
            $"        public void Stop() {{ }}\n" +
            $"    }}\n}}\n";

        private static string ReadmeContent(string name) =>
            $"# {name}\n\nAirGesture AI Plugin Sample.\n\n" +
            $"Build with: `dotnet build -c Release`\n" +
            $"Copy the DLL to the `Plugins/` folder and restart AirGesture AI.\n";

        private static void AddEntry(ZipArchive archive, string path, string content)
        {
            var entry = archive.CreateEntry(path, CompressionLevel.Optimal);
            using var writer = new StreamWriter(entry.Open(), Encoding.UTF8);
            writer.Write(content);
        }
    }
}
