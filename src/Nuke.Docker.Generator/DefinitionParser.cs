// Copyright Sebastian Karasek, Matthias Koch 2018.
// Distributed under the MIT License.
// https://github.com/nuke-build/docker/blob/master/LICENSE

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Nuke.CodeGeneration.Generators;
using Nuke.CodeGeneration.Model;
using Nuke.Docker.Generator.Utility;

namespace Nuke.Docker.Generator
{
    internal class DefinitionParser
    {
        private readonly List<CommandDefinition> _commandDefinitions;
        private readonly Dictionary<string, List<string>> _enumerations = new Dictionary<string, List<string>>();
        private readonly Tool _tool;

        private DefinitionParser(List<CommandDefinition> commandDefinitions)
        {
            _commandDefinitions = commandDefinitions;
            _tool = CreateTool(_commandDefinitions.Select(x => x.ReferenceUrl).ToList());
        }

        private Tool Generate()
        {
            GenerateTasks();
            GenerateEnumerations();
            return _tool;
        }

        private void GenerateEnumerations()
        {
            foreach (var enumeration in _enumerations)
            {
                _tool.Enumerations.Add(new Enumeration
                                       {
                                           Name = enumeration.Key,
                                           Values = enumeration.Value
                                       });
            }
        }

        private void GenerateTasks()
        {
            foreach (var definition in _commandDefinitions)
            {
                var task = new Task
                           {
                               Postfix = GetPostfix(definition),
                               Help = definition.ShortDescription.FormatForXmlDoc(),
                               Tool = _tool
                           };

                if (string.IsNullOrEmpty(task.Postfix)) continue;

                var settingsClass = new SettingsClass
                                    {
                                        Tool = _tool,
                                        Task = task,
                                        BaseClass = definition.InheritedArguments.Any()
                                            ? definition.ParentName.ToPascalCase(separator: ' ') + "Settings"
                                            : "DockerSettings"
                                    };

                AddProperties(settingsClass, definition, out var definiteArgument);

                task.DefiniteArgument = definiteArgument;
                task.SettingsClass = settingsClass;
                _tool.Tasks.Add(task);
            }
        }

        private void AddProperties(DataClass settingsClass, CommandDefinition definition, out string definiteArgument)
        {
            var usageParams = UsageParser.Parse(definition.Usage);
            definiteArgument = string.Empty;
            foreach (var usageParam in usageParams)
            {
                if (!usageParam.IsArgument)
                {
                    if (usageParam.RawValue == "docker") continue;
                    definiteArgument += " " + usageParam.RawValue;
                    continue;
                }

                if (usageParam.RawValue == "[OPTIONS]")
                {
                    definition.Arguments.ForEach(argument => { AddProperty(settingsClass, argument); });
                    continue;
                }

                settingsClass.Properties.Add(new Property
                                             {
                                                 Name = usageParam.IsList || usageParam.IsDictionary ? usageParam.Name.ToPlural() : usageParam.Name,
                                                 DataClass = settingsClass,
                                                 Format = "{value}",
                                                 Help = GetPositionalArgumentHelp(usageParam, definition),
                                                 Type = usageParam.IsList ? "List<string>" :
                                                     usageParam.IsDictionary ? "Dictionary<string,string>" : "string",
                                                 Separator = usageParam.IsList ? ' ' : default(char?),
                                                 ItemFormat = usageParam.IsDictionary ? "{key=value}" : null
                                             });
            }

            definiteArgument = definiteArgument.Trim();
        }

        private void AddProperty(DataClass settingsClass, ArgumentDefinition argument)
        {
            var propertyName = argument.Name.ToPascalCase(separator: '-');
            var enumerations = GetEnumerationTypes(argument);
            var isEnumeration = enumerations.Any();

            var property = new Property
                           {
                               Name = propertyName,
                               Help = argument.Description.RemoveNewLines().FormatForXmlDoc(),
                               DataClass = settingsClass,
                               Format = $"--{argument.Name}={{value}}"
                           };
            if (isEnumeration)
            {
                if (!_enumerations.ContainsKey(propertyName)) _enumerations.Add(propertyName, enumerations);
                property.Type = property.Name;
            }
            else if (new[] { "list,stringSlice" }.Contains(argument.ValueType))
                property.Type = "List<string>";
            else if (argument.ValueType == "map")
            {
                property.Type = GetNukeType(argument);
                property.ItemFormat = "{key}:{value}";
            }
            else
                property.Type = GetNukeType(argument);

            settingsClass.Properties.Add(property);
        }

        public static Tool GenerateTool(List<CommandDefinition> definitions)
        {
            return new DefinitionParser(definitions).Generate();
        }

        private static Tool CreateTool(List<string> references)
        {
            var tool = new Tool
                       {
                           Name = "Docker",
                           PathExecutable = "docker",
                           License = new[]
                                     {
                                         $"Copyright Sebastian Karasek, Matthias Koch {DateTime.Now.Year}.",
                                         "Distributed under the MIT License.",
                                         "https://github.com/nuke-build/docker/blob/master/LICENSE"
                                     },
                           References = references,
                           Help =
                               "Docker is an open platform for developing, shipping, and running applications. Docker enables you to separate your applications from your infrastructure so you can deliver software quickly. With Docker, you can manage your infrastructure in the same ways you manage your applications. By taking advantage of Docker’s methodologies for shipping, testing, and deploying code quickly, you can significantly reduce the delay between writing code and running it in production.",
                           OfficialUrl = "https://www.docker.com/",
                           CommonTaskProperties = new List<Property>
                                                  {
                                                      new Property
                                                      {
                                                          Name = "CliSettings",
                                                          Type = "CliSettings",
                                                          Format = "{value}",
                                                          CustomImpl = true,
                                                          CustomValue = true
                                                      }
                                                  },
                           Enumerations = new List<Enumeration>
                                          {
                                              new Enumeration
                                              {
                                                  Name = "LogLevel",
                                                  Values = new List<string> { "debug", "info", "warn", "error", "fatal" }
                                              }
                                          }
                       };
            tool.DataClasses = new List<DataClass>
                               {
                                   new DataClass
                                   {
                                       Name = "CliSettings",
                                       Tool = tool,
                                       Properties = new List<Property>
                                                    {
                                                        new Property
                                                        {
                                                            Name = "LogLevel",
                                                            Type = "LogLevel",
                                                            Help = "Set the logging level.",
                                                            Format = "--log-level {value}"
                                                        },
                                                        new Property
                                                        {
                                                            Name = "Config",
                                                            Type = "string",
                                                            Help = "Location of client config files (default ~/.docker).",
                                                            Format = "--config {value}",
                                                            Assertion = AssertionType.Directory
                                                        },
                                                        new Property
                                                        {
                                                            Name = "Debug",
                                                            Type = "bool",
                                                            Help = "Enable debug mode.",
                                                            Format = "--debug"
                                                        },
                                                        new Property
                                                        {
                                                            Name = "TLS",
                                                            Type = "bool",
                                                            Help = "Use TLS; implied by --tlsverify.",
                                                            Format = "--tls"
                                                        },
                                                        new Property
                                                        {
                                                            Name = "TLSVerify",
                                                            Type = "bool",
                                                            Help = "Use TLS and verify the remote.",
                                                            Format = "--tlsverify"
                                                        },
                                                        new Property
                                                        {
                                                            Name = "TLSCaCert",
                                                            Type = "string",
                                                            Help = "Trust certs signed only by this CA (default ~/.docker/ca.pem).",
                                                            Format = "--tlscacert {value}"
                                                        },
                                                        new Property
                                                        {
                                                            Name = "TLSCert",
                                                            Type = "string",
                                                            Help = "Path to TLS certificate file (default ~/.docker/cert.pem).",
                                                            Format = "--tlscert {value}"
                                                        },
                                                        new Property
                                                        {
                                                            Name = "TLSKey",
                                                            Type = "string",
                                                            Help = "Path to TLS key file (default ~/.docker/key.pem).",
                                                            Format = "--tlskey {value}"
                                                        }
                                                    }
                                   }
                               };

            return tool;
        }

        private static string GetPostfix(CommandDefinition command)
        {
            var postfix = command.Command.Replace("docker", string.Empty).Trim();
            return postfix.ToPascalCase(separator: ' ').ToPascalCase(separator: '-');
        }

        private static string GetNukeType(ArgumentDefinition argument)
        {
            //Todo improve
            switch (argument.ValueType)
            {
                case "string":
                    return argument.ValueType;
                case "bool":
                case "int":
                case "float":
                case "decimal":
                    return argument.ValueType + "?";
                case "int64":
                case "bytes":
                    return "long?";
                case "list":
                case "stringSlice":
                    return "List<string>";

                case "map":
                    return "Dictionary<string,string>";
                case "uint16":
                case "uint64":
                case "uint":
                    return "int";

                case "mount":
                case "credential-spec":
                case "command":
                case "network":
                case "pref":
                case "port":
                case "secret":
                case "pem-file":
                case "external-ca":
                case "node-addr":
                case "ulimit":
                case "filter":
                case "duration":
                case "config":
                default:
                    return "string";
            }
        }

        private static List<string> GetEnumerationTypes(ArgumentDefinition argument)
        {
            var regex = new Regex("\\((\"[\\w+-]+\"(?:\\|\"[\\w+-]+\")+)\\)");
            var match = regex.Match(argument.Description);
            return !match.Success
                ? new List<string>()
                : match.Groups[groupnum: 1].Value.Split(separator: '|').Select(x => x.Trim(trimChars: '"')).ToList();
        }

        private static string GetPositionalArgumentHelp(UsageParameter usageParameter, CommandDefinition commandDefinition)
        {
            if (commandDefinition.Command == "docker secret create" && usageParameter.RawValue == "[file|-]")
                return "Path to file to create the secret from.";

            if (commandDefinition.Command == "docker config create" && usageParameter.RawValue == "[file|-]")
                return "Path to file to create the config from.";

            if (usageParameter.RawValue == "PATH|URL|-") return "Path or url where the build context is located.";
            return usageParameter.RawValue.RemoveNewLines();
        }
    }
}