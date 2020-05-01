﻿/*
 * SonarLint for Visual Studio
 * Copyright (C) 2016-2020 SonarSource SA
 * mailto:info AT sonarsource DOT com
 *
 * This program is free software; you can redistribute it and/or
 * modify it under the terms of the GNU Lesser General Public
 * License as published by the Free Software Foundation; either
 * version 3 of the License, or (at your option) any later version.
 *
 * This program is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the GNU
 * Lesser General Public License for more details.
 *
 * You should have received a copy of the GNU Lesser General Public License
 * along with this program; if not, write to the Free Software Foundation,
 * Inc., 51 Franklin Street, Fifth Floor, Boston, MA  02110-1301, USA.
 */

using System.IO;
using SonarLint.VisualStudio.Core.Helpers;

namespace SonarLint.VisualStudio.Core.Binding
{
    public class SolutionBindingFilePathGenerator : ISolutionBindingFilePathGenerator
    {
        /// <summary>
        /// Generate a solution level file-path based on <paramref name="projectKey"/> and <see cref="fileNameSuffixAndExtension"/>
        /// </summary>
        /// <param name="rootDirectoryPath">Root directory to generate the full file path under</param>
        /// <param name="projectKey">SonarQube project key to generate a file name path for</param>
        /// <param name="fileNameSuffixAndExtension">Fixed file name suffix and extension (language-specific)</param>
        public string Generate(string rootDirectoryPath, string projectKey, string fileNameSuffixAndExtension)
        {
            // Cannot use Path.ChangeExtension here because if the sonar project name contains
            // a dot (.) then everything after this will be replaced with .ruleset
            var fileName = PathHelper.EscapeFileName(projectKey + fileNameSuffixAndExtension)
                .ToLowerInvariant(); // Must be lower case - see https://github.com/SonarSource/sonarlint-visualstudio/issues/1068

            return Path.Combine(rootDirectoryPath, fileName);
        }
    }
}
