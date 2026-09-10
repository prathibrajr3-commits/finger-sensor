using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using AirGestureAI.Utilities;

namespace AirGestureAI.SDK
{
    /// <summary>
    /// Generates ready-to-compile SDK starter project templates for different plugin types.
    /// Output is a ZIP archive containing a complete C# solution, project file,
    /// sample code, unit test stubs, README, and build scripts.
    /// </summary>
    public sealed class SdkGenerator
    {
        private static readonly IReadOnlyDictionary<string, string> TemplateDescriptions =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["GesturePlugin"]    = "Adds custom gesture recognition rules.",
                ["VoicePlugin"]      = "Adds custom voice command handlers.",
                ["AIPlugin"]         = "Adds an AI intent prediction module.",
                ["AutomationPlugin"] = "Adds custom workflow automation actions.",
                ["VisionPlugin"]     = "Adds computer vision UI element processors.",
                ["AgentPlugin"]      = "Adds an autonomous AI agent.",
                ["EnterprisePlugin"] = "Adds enterprise policy extensions.",
            };

        /// <summary>
        /// Gets all supported template type keys.
        /// </summary>
        public static IEnumerable<string> SupportedTemplates => TemplateDescriptions.Keys;

        /// <summary>
        /// Generates a sample plugin project ZIP archive for the given template type.
        /// </summary>
        /// <param name="templateType">One of the keys from <see cref="SupportedTemplates"/>.</param>
        /// <param name="outputDirectory">Directory where the ZIP file will be written.</param>
        /// <param name="pluginName">The name of the generated plugin class/project.</param>
        /// <returns>The full path of the created ZIP file.</returns>
        public string Generate(string templateType, string outputDirectory, string pluginName)
        {
            if (!TemplateDescriptions.ContainsKey(templateType))
                throw new ArgumentException($"Unknown template type '{templateType}'.", nameof(templateType));

            if (string.IsNullOrWhiteSpace(pluginName))
                throw new ArgumentException("Plugin name must not be empty.", nameof(pluginName));

            Directory.CreateDirectory(outputDirectory);

            var safeName = pluginName.Replace(" ", "");
            var zipPath  = Path.Combine(outputDirectory, $"{safeName}.zip");

            using var archive = ZipFile.Open(zipPath, ZipArchiveMode.Create);

            // Solution file
            AddEntry(archive, $"{safeName}.sln",      BuildSolution(safeName));
            // Project file
            AddEntry(archive, $"{safeName}/{safeName}.csproj", BuildCsproj(safeName));
            // Main plugin source
            AddEntry(archive, $"{safeName}/Plugin.cs",         BuildPluginSource(safeName, templateType));
            // Unit test project
            AddEntry(archive, $"{safeName}.Tests/{safeName}.Tests.csproj", BuildTestCsproj(safeName));
            AddEntry(archive, $"{safeName}.Tests/PluginTests.cs",           BuildTestSource(safeName));
            // README
            AddEntry(archive, "README.md", BuildReadme(safeName, templateType, TemplateDescriptions[templateType]));
            // Build script
            AddEntry(archive, "build.cmd", "dotnet build -c Release\r\n");

            Logger.Info($"SdkGenerator: Created '{templateType}' template → '{zipPath}'");
            return zipPath;
        }

        // ── Template Builders ────────────────────────────────────────────────

        private static string BuildSolution(string name) =>
            $$"""
            Microsoft Visual Studio Solution File, Format Version 12.00
            Project("{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}") = "{{name}}", "{{name}}\{{name}}.csproj", "{00000001-0000-0000-0000-000000000001}"
            EndProject
            """;

        private static string BuildCsproj(string name) =>
            $"""
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <TargetFramework>net8.0-windows</TargetFramework>
                <AssemblyName>{name}</AssemblyName>
                <RootNamespace>{name}</RootNamespace>
                <Nullable>enable</Nullable>
                <ImplicitUsings>enable</ImplicitUsings>
              </PropertyGroup>
              <ItemGroup>
                <!-- Reference the AirGesture AI SDK NuGet package or local DLL -->
                <!-- <PackageReference Include="AirGestureAI.SDK" Version="1.*" /> -->
              </ItemGroup>
            </Project>
            """;

        private static string BuildPluginSource(string name, string templateType) =>
            $$"""
            using System;

            namespace {{name}}
            {
                /// <summary>
                /// {{name}} — a {{templateType}} for AirGesture AI.
                /// </summary>
                public sealed class {{name}}Plugin
                {
                    public string Name    => "{{name}}";
                    public string Version => "1.0.0";

                    public bool Initialize()
                    {
                        Console.WriteLine($"{Name} v{Version} initialized.");
                        return true;
                    }

                    public void Start()  => Console.WriteLine($"{Name} started.");
                    public void Stop()   => Console.WriteLine($"{Name} stopped.");
                    public void Dispose() { }
                }
            }
            """;

        private static string BuildTestCsproj(string name) =>
            $"""
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <TargetFramework>net8.0</TargetFramework>
                <IsPackable>false</IsPackable>
              </PropertyGroup>
              <ItemGroup>
                <PackageReference Include="Microsoft.NET.Test.Sdk" Version="17.*" />
                <PackageReference Include="xunit"                    Version="2.*" />
                <PackageReference Include="xunit.runner.visualstudio" Version="2.*" />
                <ProjectReference Include="../{name}/{name}.csproj" />
              </ItemGroup>
            </Project>
            """;

        private static string BuildTestSource(string name) =>
            $$"""
            using Xunit;

            namespace {{name}}.Tests
            {
                public class PluginTests
                {
                    [Fact]
                    public void Initialize_ShouldReturnTrue()
                    {
                        var plugin = new {{name}}Plugin();
                        Assert.True(plugin.Initialize());
                    }
                }
            }
            """;

        private static string BuildReadme(string name, string type, string description) =>
            $"""
            # {name}

            **Type**: {type}
            **Description**: {description}

            ## Getting Started

            1. Open `{name}.sln` in Visual Studio 2022 or later.
            2. Restore NuGet packages and build the solution.
            3. Copy the output DLL to the AirGesture AI `Plugins/` directory.
            4. Restart AirGesture AI — the plugin will be auto-discovered.

            ## SDK Reference

            See the [AirGesture AI Developer Center] inside the application for full API documentation.
            """;

        private static void AddEntry(ZipArchive archive, string entryName, string content)
        {
            var entry = archive.CreateEntry(entryName, CompressionLevel.Optimal);
            using var writer = new StreamWriter(entry.Open());
            writer.Write(content);
        }
    }
}
