// Copyright Sebastian Karasek, Matthias Koch 2018.
// Distributed under the MIT License.
// https://github.com/nuke-build/docker/blob/master/LICENSE

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Nuke.Core;
using Nuke.Core.Tooling;

namespace Nuke.Docker
{
    [Serializable]
    public abstract class DockerSettings : ToolSettings
    {
        public CliSettings CliSettings { get; internal set; }

        protected string GetCliSettings()
        {
            return string.Empty;
        }

        protected override Arguments ConfigureArguments(Arguments arguments)
        {
            var secretFieldInfo = typeof(Arguments).GetField("_secrets", BindingFlags.Instance | BindingFlags.NonPublic);
            var argumentFieldInfo =
                typeof(Arguments).GetField("_arguments", BindingFlags.Instance | BindingFlags.NonPublic);

            var args = (LookupTable<string, string>) argumentFieldInfo.NotNull().GetValue(arguments);
            var secrets = (List<string>) secretFieldInfo.NotNull().GetValue(arguments);

            var newArgs = CliSettings == null ? new Arguments() : CliSettings.CreateArguments();
            var newArgsArgs = (LookupTable<string, string>) argumentFieldInfo.GetValue(newArgs);
            var newArgsSecrets = (List<string>) secretFieldInfo.GetValue(newArgs);

            foreach (var arg in args) newArgsArgs.AddRange(arg.Key, arg.ToArray());
            newArgsSecrets.AddRange(secrets);

            return base.ConfigureArguments(newArgs);
        }
    }
}