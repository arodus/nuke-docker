﻿// Copyright Sebastian Karasek, Matthias Koch 2018.
// Distributed under the MIT License.
// https://github.com/nuke-build/docker/blob/master/LICENSE

using System;
using System.Linq;
using Newtonsoft.Json;
using Nuke.Common.IO;

namespace Nuke.Docker.Generator
{
    public static class SpecificationGenerator
    {
        public static void GenerateSpecifications(SpecificationGeneratorSettings settings)
        {
            Console.WriteLine($"Generating docker specifications...");
            var definitions =
                DefinitionFetcher.GetCommandDefinitionsFromFolder(settings.DefinitonFolder, settings.Reference, settings.CommandsToSkip);
            var tool = DefinitionParser.GenerateTool(definitions);

            var specification = JsonConvert.SerializeObject(tool,
                new JsonSerializerSettings
                {
                    NullValueHandling = NullValueHandling.Ignore,
                    Formatting = Formatting.Indented,
                    DefaultValueHandling = DefaultValueHandling.Include
                });

            TextTasks.WriteAllText(PathConstruction.Combine(settings.OutputFolder, "Docker.json"), specification);
            Console.WriteLine();
            Console.WriteLine("Generation finished.");
            Console.WriteLine($"Created Tasks: {tool.Tasks.Count}");
            Console.WriteLine($"Created Data Classes: {tool.DataClasses.Count}");
            Console.WriteLine($"Created Enumerations: {tool.Enumerations.Count}");
            Console.WriteLine($"Created Common Task Properties: {tool.CommonTaskProperties.Count}");
            Console.WriteLine($"Created Common Task Property Sets: {tool.CommonTaskPropertySets.Count}");
        }
    }
}