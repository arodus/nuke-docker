// Copyright Sebastian Karasek, Matthias Koch 2018.
// Distributed under the MIT License.
// https://github.com/nuke-build/docker/blob/master/LICENSE

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;

namespace Nuke.Docker.Generator
{
    internal class Program
    {
        private static void Main()
        {
            var args = Environment.GetCommandLineArgs();

            foreach(var arg in args){
                Console.WriteLine(arg);
            }

            //Nuke dot net run fix
            if (args.Length == 2 && args[1].Contains(' '))
            {
                var newArgs = new List<string> { args[0] };
                newArgs.AddRange(args[1].Trim('\"').Split(' ').ToList());
                args = newArgs.ToArray();
            }

            var outputPath = args[1];
            var skip = args.Skip(count: 2).SingleOrDefault(x => x.StartsWith("--skip="))?.Substring(startIndex: 7).Split(separator: '+')
                       ?? new string[0];
            var branch = args.Skip(count: 2).SingleOrDefault(x => x.StartsWith("--branch="))?.Substring(startIndex: 9) ?? "master";

            Console.WriteLine(string.Empty);
            Console.WriteLine($"Generating docker nuke tools metadata from branch: {branch}");
            Console.WriteLine($"Commands to skip:{skip.Aggregate(string.Empty,(current,next) => current += $" {next}")}");
            Console.WriteLine($"Output path: {outputPath}");

            var definitionsTask = DefinitionFetcher.GetCommandDefinitionsFromGitHub(branch, skip);
            definitionsTask.Wait();

            var tool = DockerGenerator.GenerateTool(definitionsTask.Result);

            File.WriteAllText(outputPath,
                JsonConvert.SerializeObject(tool, new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore, Formatting = Formatting.Indented, DefaultValueHandling = DefaultValueHandling.Include}));

            Console.WriteLine();
            Console.WriteLine("Generation finished.");
            Console.WriteLine($"Created Tasks: {tool.Tasks.Count}");
            Console.WriteLine($"Created data classed: {tool.DataClasses.Count}");
            Console.WriteLine($"Created enumerations: {tool.Enumerations.Count}");
        }
    }
}