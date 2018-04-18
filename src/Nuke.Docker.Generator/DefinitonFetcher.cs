// Copyright Sebastian Karasek, Matthias Koch 2018.
// Distributed under the MIT License.
// https://github.com/nuke-build/docker/blob/master/LICENSE

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Octokit;
using YamlDotNet.Serialization;

namespace Nuke.Docker.Generator
{
    internal static class DefinitionFetcher
    {
        private const string c_dockerRepositoryOwner = "Docker";
        private const string c_dockerDocRepositoryName = "docker.github.io";
        private const string c_dockerCliDefinitionPath = "_data/engine-cli";

        public static List<CommandDefinition> GetCommandDefinitionsFromFolder(
            string path,
            params string[] commandsToSkip)
        {
            var files = Directory.EnumerateFiles(path, "*.yaml").ToList();
            var definitions = new List<CommandDefinition>();

            foreach (var file in files)
            {
                if (commandsToSkip.Contains(Path.GetFileNameWithoutExtension(file))) continue;
                definitions.Add(ParseDefinition(File.ReadAllText(file), file));
            }

            return definitions;
        }

        public static async Task<List<CommandDefinition>> GetCommandDefinitionsFromGitHub(string branch, params string[] commandsToSkip)
        {
            var definitions = await GetDefinitionUrls(branch, commandsToSkip);
            using (var client = new HttpClient())
            {
                // ReSharper disable AccessToDisposedClosure
                return (await Task.WhenAll(definitions.Where(x => !commandsToSkip.Contains(x.Key.Replace(".yaml", string.Empty)))
                        .Select(x => DownloadDefinition(x.Value, client))))
                    .Select(x => ParseDefinition(x.Value, x.Key)).ToList();
                // ReSharper restore AccessToDisposedClosure
            }
        }

        private static async Task<IEnumerable<KeyValuePair<string, string>>> GetDefinitionUrls(string branch, params string[] commandsToSkip)
        {
            var client = new GitHubClient(new ProductHeaderValue("Nuke.Docker.Generator"));
            var content = await client.Repository.Content.GetAllContentsByRef(c_dockerRepositoryOwner,
                c_dockerDocRepositoryName,
                c_dockerCliDefinitionPath,
                branch);

            return content.Where(x => !commandsToSkip.Contains(x.Name)).Select(x => new KeyValuePair<string, string>(x.Name, x.DownloadUrl));
        }

        private static async Task<KeyValuePair<string, string>> DownloadDefinition(string url, HttpClient client)
        {
            var response = await client.GetAsync(url);
            response.EnsureSuccessStatusCode();
            var definitionYaml = await response.Content.ReadAsStringAsync();
            return new KeyValuePair<string, string>(url, definitionYaml);
        }

        private static CommandDefinition ParseDefinition(string definitionYaml, string reference)
        {
            var definiton = new Deserializer().Deserialize<CommandDefinition>(definitionYaml);
            definiton.ReferenceUrl = reference;
            return definiton;
        }
    }
}