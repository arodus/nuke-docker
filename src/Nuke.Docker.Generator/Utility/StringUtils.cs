// Copyright Sebastian Karasek, Matthias Koch 2018.
// Distributed under the MIT License.
// https://github.com/nuke-build/docker/blob/master/LICENSE

using System;
using System.Linq;
using JetBrains.Annotations;

namespace Nuke.Docker.Generator.Utility
{
    internal static class StringUtils
    {
        [Pure]
        [CanBeNull]
        public static string RemoveNewLines([CanBeNull] this string value)
        {
            return value?.Replace("\r", string.Empty).Replace("\n", string.Empty);
        }

        [Pure]
        [CanBeNull]
        public static string ToCamelCase([CanBeNull] this string value, char separator)
        {
            if (value == null) return null;

            var index = value.IndexOf(separator);
            while (index > 0)
            {
                value = value.Substring(startIndex: 0, length: index) + char.ToUpper(value[index + 1]) + value.Substring(index + 2);
                index = value.IndexOf(separator);
            }

            return value;
        }

        [Pure]
        [CanBeNull]
        public static string ToPascalCase([CanBeNull] this string value, char separator)
        {
            var camelCase = value.ToCamelCase(separator);
            if (string.IsNullOrEmpty(camelCase)) return camelCase;
            return char.ToUpper(camelCase[index: 0]) + camelCase.Substring(startIndex: 1);
        }

        [Pure]
        [CanBeNull]
        public static string AddTailingPeriodIfNeeded([CanBeNull] this string value)
        {
            if (string.IsNullOrEmpty(value)) return value;
            return value[value.Length - 1] == '.' ? value : value + '.';
        }

        [Pure]
        [CanBeNull]
        public static string EscapeForXmlDoc([CanBeNull] this string value)
        {
            if (string.IsNullOrEmpty(value)) return value;
            return value.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;");
        }

        [Pure]
        [CanBeNull]
        public static string FormatForXmlDoc([CanBeNull] this string value)
        {
            return value.EscapeForXmlDoc().AddTailingPeriodIfNeeded();
        }
    }
}