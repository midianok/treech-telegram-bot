using System.Globalization;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using OpenAI.Chat;

namespace Saturn.Bot.Service.Services.Prompts;

/// <summary>
/// Prompts and AI tool descriptions kept as Markdown files in the <c>Prompts</c> folder and embedded into the assembly.
/// A prompt is addressed by its path without the extension: <c>"Chat/Behavior"</c> is <c>Prompts/Chat/Behavior.md</c>.
/// </summary>
/// <remarks>
/// Template syntax:
/// <list type="bullet">
/// <item><c>{{name}}</c> — a parameter taken from the <c>args</c> object (an anonymous object or a dictionary);</item>
/// <item><c>{{&gt; Shared/Name}}</c> — inserts another prompt file, rendered with the same parameters;</item>
/// <item><c>&lt;!-- ... --&gt;</c> — a comment for whoever edits the file, never sent to the model.</item>
/// </list>
/// Parameters are checked strictly: a missing or an unused one throws, so a typo shows up on the first call.
/// Substituted values are not parsed again, so user text with braces is safe.
/// </remarks>
public static partial class PromptLibrary
{
    private const string ResourcePrefix = "Prompts/";
    private const string Extension = ".md";
    private const string ToolsFolder = "Tools/";

    private static readonly IReadOnlyDictionary<string, string> Templates = LoadTemplates();
    private static readonly IReadOnlyDictionary<string, object?> NoArgs = new Dictionary<string, object?>();

    /// <summary>Renders the prompt <paramref name="name"/> with the given parameters.</summary>
    /// <example><c>PromptLibrary.Get("Chat/CurrentSender", new { sender = "Вася (@vasya)" })</c></example>
    public static string Get(string name, object? args = null)
    {
        var values = ToDictionary(args);
        var used = new HashSet<string>();
        var text = Render(name, values, used, []);

        var unused = values.Keys.Where(key => !used.Contains(key)).ToList();
        if (unused.Count > 0)
        {
            throw new InvalidOperationException($"Prompt '{name}' doesn't use parameters: {string.Join(", ", unused)}");
        }

        return text;
    }

    /// <summary>
    /// Builds a tool definition from <c>Prompts/Tools/{functionName}.md</c> and a JSON schema without descriptions.
    /// The text before the first <c>## heading</c> describes the function, each <c>## parameter_name</c> section
    /// describes the property of the same name. Every property must be described and every section must match a property.
    /// </summary>
    public static ChatTool Tool(string functionName, object parameters, object? args = null)
    {
        var name = ToolsFolder + functionName;
        var (description, sections) = SplitSections(Get(name, args));

        var schema = JsonSerializer.SerializeToNode(parameters) as JsonObject
                     ?? throw new InvalidOperationException($"Tool '{functionName}' parameters must be a JSON object");
        var properties = schema["properties"] as JsonObject ?? new JsonObject();

        var undescribed = properties.Select(p => p.Key).Where(key => !sections.ContainsKey(key)).ToList();
        if (undescribed.Count > 0)
        {
            throw new InvalidOperationException($"Prompt '{name}' has no sections for parameters: {string.Join(", ", undescribed)}");
        }

        foreach (var (key, text) in sections)
        {
            if (properties[key] is not JsonObject property)
            {
                throw new InvalidOperationException($"Prompt '{name}' describes unknown parameter '{key}'");
            }

            property["description"] = text;
        }

        return ChatTool.CreateFunctionTool(functionName, description, BinaryData.FromString(schema.ToJsonString()));
    }

    private static string Render(string name, IReadOnlyDictionary<string, object?> args, HashSet<string> used, IReadOnlyList<string> includedFrom)
    {
        if (!Templates.TryGetValue(name, out var template))
        {
            throw new KeyNotFoundException($"Prompt '{name}' not found, expected file {ResourcePrefix}{name}{Extension}");
        }

        if (includedFrom.Contains(name))
        {
            throw new InvalidOperationException($"Prompt include cycle: {string.Join(" -> ", includedFrom)} -> {name}");
        }

        return PlaceholderRegex().Replace(template, match =>
        {
            var key = match.Groups["key"].Value;
            if (match.Groups["include"].Success)
            {
                return Render(key, args, used, [..includedFrom, name]);
            }

            if (!args.TryGetValue(key, out var value))
            {
                throw new InvalidOperationException($"Prompt '{name}' requires parameter '{key}'");
            }

            used.Add(key);
            return Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty;
        });
    }

    private static (string Description, IReadOnlyDictionary<string, string> Sections) SplitSections(string text)
    {
        var headings = SectionRegex().Matches(text);
        if (headings.Count == 0)
        {
            return (text, new Dictionary<string, string>());
        }

        var sections = new Dictionary<string, string>();
        for (var i = 0; i < headings.Count; i++)
        {
            var key = headings[i].Groups["key"].Value;
            var start = headings[i].Index + headings[i].Length;
            var end = i + 1 < headings.Count ? headings[i + 1].Index : text.Length;
            if (!sections.TryAdd(key, text[start..end].Trim()))
            {
                throw new InvalidOperationException($"Tool prompt describes parameter '{key}' twice");
            }
        }

        return (text[..headings[0].Index].Trim(), sections);
    }

    private static IReadOnlyDictionary<string, object?> ToDictionary(object? args) => args switch
    {
        null => NoArgs,
        IReadOnlyDictionary<string, object?> dictionary => dictionary,
        _ => args.GetType()
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .ToDictionary(property => property.Name, property => property.GetValue(args))
    };

    private static Dictionary<string, string> LoadTemplates()
    {
        var assembly = typeof(PromptLibrary).Assembly;
        var templates = new Dictionary<string, string>();
        foreach (var resource in assembly.GetManifestResourceNames())
        {
            var path = resource.Replace('\\', '/');
            if (!path.StartsWith(ResourcePrefix, StringComparison.Ordinal) || !path.EndsWith(Extension, StringComparison.Ordinal))
            {
                continue;
            }

            using var stream = assembly.GetManifestResourceStream(resource)!;
            using var reader = new StreamReader(stream);
            var text = CommentRegex().Replace(reader.ReadToEnd().ReplaceLineEndings("\n"), string.Empty).Trim();
            templates[path[ResourcePrefix.Length..^Extension.Length]] = text;
        }

        return templates;
    }

    [GeneratedRegex(@"\{\{\s*(?<include>>\s*)?(?<key>[\w/.-]+)\s*\}\}")]
    private static partial Regex PlaceholderRegex();

    [GeneratedRegex(@"^##[ \t]+(?<key>\w+)[ \t]*$", RegexOptions.Multiline)]
    private static partial Regex SectionRegex();

    [GeneratedRegex(@"<!--.*?-->", RegexOptions.Singleline)]
    private static partial Regex CommentRegex();
}
