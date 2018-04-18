// Copyright Sebastian Karasek, Matthias Koch 2018.
// Distributed under the MIT License.
// https://github.com/nuke-build/docker/blob/master/LICENSE

using System;
using System.Linq;
using Nuke.Docker.Generator.Utility;

namespace Nuke.Docker.Generator
{
    public class UsageParameter
    {
        public string RawValue { get; internal set; }

        public bool IsArgument => IsList || IsDictionary || RawValue.EndsWith(value: ']') || RawValue.Contains(value: '|')
                                  || RawValue.All(x => char.IsUpper(x) || x == '_' || x == '-');

        public bool IsList { get; internal set; }
        public bool IsDictionary { get; internal set; }

        public string Name => IsDictionary
            ? TrimmedValue.ToLowerInvariant().ToPascalCase(separator: '=')
            : TrimmedValue.Split(new[] { ':', '|', '[' }, StringSplitOptions.RemoveEmptyEntries).First().ToLowerInvariant()
                .ToPascalCase(separator: '-').ToPascalCase(separator: '_');

        private string TrimmedValue => RawValue.Trim('[', ']', '.', ':', '@', '/', '(', ')');
    }
}