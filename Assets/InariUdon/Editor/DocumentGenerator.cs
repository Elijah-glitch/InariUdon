using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using EsnyaFactory.InariUdon.Documentation;
using UdonSharp;
using UdonToolkit;
using UnityEditor;
using UnityEngine;
using VRC.Udon.Serialization.OdinSerializer.Utilities;

namespace EsnyaFactory.InariUdon
{
    public class DocumentGenerator
    {
        private const string HeaderString = "# InariUdon Components\n\n";
        private const string VariablesHeaderString = "\n#### Public Variables\n| Name | Type | Description |\n|:--|:--|:--|";
        private const string EventsHeaderString = "\n#### Public Events\n| Name | Description |\n|:--|:--|";
        private const BindingFlags bindingFlags = BindingFlags.DeclaredOnly | BindingFlags.Public | BindingFlags.Instance;
        private static readonly string[] eventBlacklist = typeof(UdonSharpBehaviour)
            .GetMethods(bindingFlags)
            .Select(m => m.Name)
            .ToArray();
        private static readonly Regex eventExcludePattern = new Regex("^Editor");

        private static void WriteText(string relativePath, string contents, [CallerFilePath] string filePath = "")
        {
            var path = Path.Combine(Path.GetDirectoryName(filePath), relativePath);
            // Debug.Log(Path.GetFullPath(path));
            File.WriteAllText(path, contents);
        }

        private static IEnumerable<string> GenerateHeader(Type type)
        {
            var name = type.GetCustomAttribute<CustomNameAttribute>()?.name ?? type.Name;
            var description = type.GetCustomAttribute<HelpMessageAttribute>()?.helpMessage ?? "";
            return Enumerable.Repeat($"\n### {name}", 1).Append(description);
        }

        private static IEnumerable<string> GenerateVariables(Type type)
        {
            var items = type.GetFields(bindingFlags).Where(p => !p.HasCustomAttribute<HideInInspector>()).ToArray();
            if (items.Length == 0) return Enumerable.Empty<string>();

            var variables = items.Select(v => {
                var name = v.Name;
                var typeName = v.FieldType.ToString();
                var description = v.GetAttribute<HelpBoxAttribute>()?.text ?? v.GetAttribute<TooltipAttribute>()?.tooltip ?? "";
                return $"| {name} | {typeName} | {description} |";
            });
            return Enumerable.Repeat(VariablesHeaderString, 1).Concat(variables);
        }

        private static IEnumerable<string> GenerateEvents(Type type)
        {
            var methods = type.GetMethods(bindingFlags).Where(m => !eventBlacklist.Contains(m.Name) && !eventExcludePattern.IsMatch(m.Name)).ToArray();
            if (methods.Length == 0) return Enumerable.Empty<string>();

            var events = methods
                .Where(m => m.GetParameters().FirstOrDefault() == null)
                .Select(m => {
                    var name = m.Name;
                    var description = m.GetAttribute<Documentation.EventDescriptionAttribute>()?.description ?? "";
                    return $"| {name} | {description} |";
                });
            return Enumerable.Repeat(EventsHeaderString, 1).Concat(events);
        }

        private static IEnumerable<string> GenerateImageAttachments(Type type)
        {
            return type.GetAttribute<ImageAttachments>()?.urls?.Select(url => $"![image]({url})") ?? Enumerable.Empty<string>();
        }

        [MenuItem("EsnyaTools/InariUdon/Generate Documents")]
        private static void GenerateDocuments()
        {
            var doc = AppDomain.CurrentDomain.GetAssemblies()
                .SelectMany(a => a.GetTypes())
                .Where(t => t.Namespace?.StartsWith("EsnyaFactory.InariUdon") ?? false)
                .Where(t => t.ImplementsOrInherits(typeof(UdonSharpBehaviour)))
                .OrderBy(t => t.Namespace)
                .GroupBy(
                    t => t.Namespace.Split('.').Skip(2).Append("Uncategorized").FirstOrDefault(),
                    (key, items) => Enumerable.Repeat($"\n## {key}", 1)
                        .Concat(
                            items.SelectMany(
                                c => GenerateHeader(c)
                                .Concat(GenerateVariables(c))
                                .Concat(GenerateEvents(c))
                                .Concat(GenerateImageAttachments(c))
                            )
                        )
                )
                .SelectMany(e => e)
                .Aggregate(HeaderString, (a, b) => $"{a}\n{b}");

            WriteText("../../../COMPONENTS.md", doc);
        }
    }
}
