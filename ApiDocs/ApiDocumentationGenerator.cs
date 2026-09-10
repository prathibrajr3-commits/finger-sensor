using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using AirGestureAI.Utilities;

namespace AirGestureAI.ApiDocs
{
    /// <summary>
    /// Generates API documentation by reflecting over loaded assemblies and
    /// extracting XML documentation comments to produce structured API trees.
    /// </summary>
    public sealed class ApiDocumentationGenerator
    {
        private readonly DocumentationBuilder _builder;
        private readonly MarkdownExporter _mdExporter;
        private readonly HtmlDocumentationExporter _htmlExporter;

        /// <summary>
        /// Initializes a new <see cref="ApiDocumentationGenerator"/>.
        /// </summary>
        public ApiDocumentationGenerator()
        {
            _builder      = new DocumentationBuilder();
            _mdExporter   = new MarkdownExporter();
            _htmlExporter = new HtmlDocumentationExporter();
        }

        /// <summary>
        /// Reflects over the given assembly and builds the API documentation tree.
        /// </summary>
        /// <param name="assembly">The assembly to document.</param>
        /// <returns>A list of <see cref="ApiTypeDoc"/> entries.</returns>
        public IReadOnlyList<ApiTypeDoc> BuildFromAssembly(Assembly assembly)
        {
            Logger.Info($"ApiDocumentationGenerator: Reflecting '{assembly.GetName().Name}'…");
            return _builder.Build(assembly);
        }

        /// <summary>
        /// Generates Markdown documentation for the given assembly and writes it to a file.
        /// </summary>
        public string ExportMarkdown(Assembly assembly, string outputPath)
        {
            var docs = BuildFromAssembly(assembly);
            var md   = _mdExporter.Export(docs, assembly.GetName().Name ?? "API");
            var dir  = Path.GetDirectoryName(outputPath);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
            File.WriteAllText(outputPath, md, Encoding.UTF8);
            Logger.Info($"ApiDocumentationGenerator: Markdown written → '{outputPath}'");
            return outputPath;
        }

        /// <summary>
        /// Generates HTML documentation for the given assembly and writes it to a file.
        /// </summary>
        public string ExportHtml(Assembly assembly, string outputPath)
        {
            var docs = BuildFromAssembly(assembly);
            var html = _htmlExporter.Export(docs, assembly.GetName().Name ?? "API");
            var dir  = Path.GetDirectoryName(outputPath);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
            File.WriteAllText(outputPath, html, Encoding.UTF8);
            Logger.Info($"ApiDocumentationGenerator: HTML written → '{outputPath}'");
            return outputPath;
        }
    }

    /// <summary>Represents documentation for a single public type.</summary>
    public sealed class ApiTypeDoc
    {
        /// <summary>Gets the fully qualified type name.</summary>
        public string FullName { get; init; } = string.Empty;

        /// <summary>Gets the short type name.</summary>
        public string Name { get; init; } = string.Empty;

        /// <summary>Gets the XML summary extracted from documentation comments.</summary>
        public string Summary { get; init; } = string.Empty;

        /// <summary>Gets the namespace this type belongs to.</summary>
        public string Namespace { get; init; } = string.Empty;

        /// <summary>Gets the documented public members.</summary>
        public IReadOnlyList<ApiMemberDoc> Members { get; init; } = Array.Empty<ApiMemberDoc>();
    }

    /// <summary>Represents documentation for a single public member (method, property, etc.).</summary>
    public sealed class ApiMemberDoc
    {
        /// <summary>Gets the member name.</summary>
        public string Name { get; init; } = string.Empty;

        /// <summary>Gets the member type (Method, Property, Event, Field).</summary>
        public string MemberType { get; init; } = string.Empty;

        /// <summary>Gets the XML summary extracted from documentation comments.</summary>
        public string Summary { get; init; } = string.Empty;

        /// <summary>Gets the return type name (for methods).</summary>
        public string ReturnType { get; init; } = string.Empty;

        /// <summary>Gets whether this member is marked with <c>[Obsolete]</c>.</summary>
        public bool IsDeprecated { get; init; }
    }
}
