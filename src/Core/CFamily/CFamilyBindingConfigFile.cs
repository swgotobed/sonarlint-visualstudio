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

using System;
using System.IO.Abstractions;
using Newtonsoft.Json;
using SonarLint.VisualStudio.Core.Binding;

namespace SonarLint.VisualStudio.Core.CFamily
{
    public class CFamilyBindingConfigFile : IBindingConfigFile
    {
        private readonly IFileSystem fileSystem;
        public string FilePath { get; }

        public CFamilyBindingConfigFile(RulesSettings ruleSettings, string filePath)
            : this (ruleSettings, filePath, new FileSystem())
        {
        }

        public CFamilyBindingConfigFile(RulesSettings rulesSettings, string filePath, IFileSystem fileSystem)
        {
            RuleSettings = rulesSettings ?? throw new ArgumentNullException(nameof(rulesSettings));
            FilePath = filePath ?? throw new ArgumentNullException(nameof(filePath));
            this.fileSystem = fileSystem ?? throw new ArgumentNullException(nameof(fileSystem));
        }

        internal /* for testing */ RulesSettings RuleSettings { get; }

        public void Save()
        {
            if (string.IsNullOrEmpty(FilePath))
            {
                throw new ArgumentNullException(nameof(FilePath));
            }

            var dataAsText = JsonConvert.SerializeObject(RuleSettings, Formatting.Indented);
            fileSystem.File.WriteAllText(FilePath, dataAsText);
        }
    }
}
