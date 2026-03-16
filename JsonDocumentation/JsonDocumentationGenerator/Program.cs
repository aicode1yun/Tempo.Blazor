using System.Text.Json;
using System.Text.Json.Nodes;

namespace JsonDocumentationGenerator;

class Program
{
    static int Main(string[] args)
    {
        // Determine base directory: either passed as argument or auto-detected
        string baseDir;
        if (args.Length > 0 && Directory.Exists(args[0]))
        {
            baseDir = Path.GetFullPath(args[0]);
        }
        else
        {
            // Default: look for JsonDocumentation folder relative to current directory
            baseDir = FindJsonDocumentationDir(Directory.GetCurrentDirectory());
            if (baseDir == null)
            {
                Console.Error.WriteLine("Error: Could not find JsonDocumentation directory.");
                Console.Error.WriteLine("Usage: JsonDocumentationGenerator [path-to-JsonDocumentation-folder] [output-file]");
                return 1;
            }
        }

        // Check for --enrich flag: update component JSONs with parameters/kind/category from source
        if (args.Any(a => a == "--enrich"))
        {
            var srcDir = Path.Combine(baseDir, "..", "src", "Tempo.Blazor", "Components");
            srcDir = Path.GetFullPath(srcDir);
            var jsonDir = Path.Combine(baseDir, "Components");

            if (!Directory.Exists(srcDir))
            {
                Console.Error.WriteLine($"Error: Source components directory not found: {srcDir}");
                return 1;
            }

            Console.WriteLine($"Enriching JSON documentation from source: {srcDir}");
            var count = ParameterEnricher.Run(srcDir, jsonDir);
            Console.WriteLine($"\nEnriched {count} component files.");
            return 0;
        }

        var componentsDir = Path.Combine(baseDir, "Components");
        var abstractionsDir = Path.Combine(baseDir, "Abstractions");
        var gettingStartedFile = Path.Combine(baseDir, "gettingStarted.json");
        var libraryExamplesFile = Path.Combine(baseDir, "libraryExamples.json");

        // Validate required files exist
        if (!Directory.Exists(componentsDir))
        {
            Console.Error.WriteLine($"Error: Components directory not found: {componentsDir}");
            return 1;
        }
        if (!File.Exists(gettingStartedFile))
        {
            Console.Error.WriteLine($"Error: gettingStarted.json not found: {gettingStartedFile}");
            return 1;
        }
        if (!File.Exists(libraryExamplesFile))
        {
            Console.Error.WriteLine($"Error: libraryExamples.json not found: {libraryExamplesFile}");
            return 1;
        }

        var jsonOptions = new JsonSerializerOptions
        {
            WriteIndented = true,
            Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
        };

        // 1. Read gettingStarted.json
        Console.WriteLine($"Reading: {gettingStartedFile}");
        var gettingStarted = JsonNode.Parse(File.ReadAllText(gettingStartedFile));

        // 2. Read all component JSON files
        var componentFiles = Directory.GetFiles(componentsDir, "*.json")
            .OrderBy(f => Path.GetFileNameWithoutExtension(f), StringComparer.OrdinalIgnoreCase)
            .ToList();

        Console.WriteLine($"Found {componentFiles.Count} component files in: {componentsDir}");

        var items = new JsonArray();
        foreach (var file in componentFiles)
        {
            var componentJson = JsonNode.Parse(File.ReadAllText(file));
            if (componentJson != null)
            {
                items.Add(componentJson);
                Console.WriteLine($"  + {Path.GetFileNameWithoutExtension(file)}");
            }
        }

        // 3. Read all abstractions JSON files (if directory exists)
        var abstractions = new JsonArray();
        if (Directory.Exists(abstractionsDir))
        {
            var abstractionFiles = Directory.GetFiles(abstractionsDir, "*.json")
                .OrderBy(f => Path.GetFileNameWithoutExtension(f), StringComparer.OrdinalIgnoreCase)
                .ToList();

            Console.WriteLine($"Found {abstractionFiles.Count} abstraction files in: {abstractionsDir}");

            foreach (var file in abstractionFiles)
            {
                var abstractionJson = JsonNode.Parse(File.ReadAllText(file));
                if (abstractionJson != null)
                {
                    abstractions.Add(abstractionJson);
                    Console.WriteLine($"  + {Path.GetFileNameWithoutExtension(file)}");
                }
            }
        }
        else
        {
            Console.WriteLine($"Abstractions directory not found: {abstractionsDir} (skipping)");
        }

        // 4. Read libraryExamples.json
        Console.WriteLine($"Reading: {libraryExamplesFile}");
        var libraryExamplesRoot = JsonNode.Parse(File.ReadAllText(libraryExamplesFile));
        var libraryExamples = libraryExamplesRoot?["examples"]?.DeepClone() ?? new JsonArray();

        // 5. Determine output paths
        string outputPath;
        string abstractionsOutputPath;
        if (args.Length > 1 && !args[1].StartsWith("--"))
        {
            outputPath = Path.GetFullPath(args[1]);
            abstractionsOutputPath = Path.Combine(
                Path.GetDirectoryName(outputPath) ?? baseDir,
                "tempo-blazor-abstractions.json");
        }
        else
        {
            outputPath = Path.Combine(baseDir, "..", "tempo-blazor-documentation.json");
            outputPath = Path.GetFullPath(outputPath);
            abstractionsOutputPath = Path.Combine(baseDir, "..", "tempo-blazor-abstractions.json");
            abstractionsOutputPath = Path.GetFullPath(abstractionsOutputPath);
        }

        // 6. Compose and write main documentation JSON (components only)
        var mainOutput = new JsonObject
        {
            ["gettingStarted"] = gettingStarted?.DeepClone(),
            ["items"] = items,
            ["libraryExamples"] = libraryExamples
        };

        var mainJson = mainOutput.ToJsonString(jsonOptions);
        File.WriteAllText(outputPath, mainJson);

        Console.WriteLine();
        Console.WriteLine($"Generated: {outputPath}");
        Console.WriteLine($"  Components: {items.Count}");
        Console.WriteLine($"  Library examples: {libraryExamples.AsArray().Count}");
        Console.WriteLine($"  File size: {mainJson.Length:N0} bytes");

        // 7. Write abstractions JSON (if any abstractions exist)
        if (abstractions.Count > 0)
        {
            var abstractionsOutput = new JsonObject
            {
                ["gettingStarted"] = new JsonObject
                {
                    ["title"] = "Tempo.Blazor.Abstractions",
                    ["description"] = "Interfaces and models for Tempo.Blazor components. These abstractions have zero UI dependencies and can be safely referenced from API or service projects.",
                    ["namespace"] = "Tempo.Blazor.Interfaces, Tempo.Blazor.Models, Tempo.Blazor.Localization",
                    ["installation"] = new JsonObject
                {
                    ["nuget"] = new JsonArray { "dotnet add package Tempo.Blazor.Abstractions" }
                }
                },
                ["items"] = abstractions
            };

            var abstractionsJson = abstractionsOutput.ToJsonString(jsonOptions);
            File.WriteAllText(abstractionsOutputPath, abstractionsJson);

            Console.WriteLine();
            Console.WriteLine($"Generated: {abstractionsOutputPath}");
            Console.WriteLine($"  Abstractions: {abstractions.Count}");
            Console.WriteLine($"  File size: {abstractionsJson.Length:N0} bytes");
        }

        return 0;
    }

    /// <summary>
    /// Walks up the directory tree looking for a "JsonDocumentation" folder
    /// that contains a "Components" subfolder and gettingStarted.json.
    /// </summary>
    static string? FindJsonDocumentationDir(string startDir)
    {
        var dir = startDir;
        while (dir != null)
        {
            var candidate = Path.Combine(dir, "JsonDocumentation");
            if (Directory.Exists(candidate)
                && Directory.Exists(Path.Combine(candidate, "Components"))
                && File.Exists(Path.Combine(candidate, "gettingStarted.json")))
            {
                return candidate;
            }
            dir = Path.GetDirectoryName(dir);
        }
        return null;
    }
}
