// Copyright Sebastian Karasek, Matthias Koch 2018.
// Distributed under the MIT License.
// https://github.com/nuke-build/docker/blob/master/LICENSE

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Nuke.Docker.Generator.Model;
using YamlDotNet.Serialization;

namespace Nuke.Docker.Generator
{
    internal static class DefinitionFetcher
    {
        private const string c_referenceUrl = "https://raw.githubusercontent.com/docker/docker.github.io/{0}/_data/engine-cli/{1}";

        public static List<CommandDefinition> GetCommandDefinitionsFromFolder(
            string path,
            string reference,
            params string[] commandsToSkip)
        {
            var files = Directory.EnumerateFiles(path, "*.yaml").ToList();
            var definitions = new List<CommandDefinition>();

            foreach (var file in files)
            {
                if (commandsToSkip.Contains(Path.GetFileNameWithoutExtension(file))) continue;
                definitions.Add(ParseDefinition(File.ReadAllText(file), reference, Path.GetFileName(file)));
            }

            return definitions;
        }

        private static CommandDefinition ParseDefinition(string definitionYaml, string reference, string fileName)
        {
            var definiton = new Deserializer().Deserialize<CommandDefinition>(definitionYaml);
            definiton.ReferenceUrl = string.Format(c_referenceUrl, reference, fileName);
            return definiton;
        }
    }
}