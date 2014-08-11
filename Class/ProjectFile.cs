using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Build.Evaluation;
using SVNCompiler.Data;

/*
    Copyright (C) 2014 Nikita Bernthaler

    This program is free software: you can redistribute it and/or modify
    it under the terms of the GNU General Public License as published by
    the Free Software Foundation, either version 3 of the License, or
    (at your option) any later version.

    This program is distributed in the hope that it will be useful,
    but WITHOUT ANY WARRANTY; without even the implied warranty of
    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
    GNU General Public License for more details.

    You should have received a copy of the GNU General Public License
    along with this program.  If not, see <http://www.gnu.org/licenses/>.
*/

namespace SVNCompiler.Class
{
    internal class ProjectFile
    {
        public readonly Project Project;
        private readonly Log _log;

        public ProjectFile(string file, Log log)
        {
            try
            {
                _log = log;
                if (File.Exists(file))
                {
                    ICollection<Project> projects = ProjectCollection.GlobalProjectCollection.GetLoadedProjects(file);
                    Project = projects.Count == 0 ? new Project(file) : projects.First();
                }
            }
            catch (Exception ex)
            {
                Utility.Log(LogStatus.Error, string.Format("Error - {0}", ex.Message), _log);
            }
        }

        public bool PrebuildEvent { get; set; }
        public bool PostbuildEvent { get; set; }
        public string Configuration { get; set; }
        public string PlatformTarget { get; set; }
        public bool UpdateReferences { get; set; }
        public string ReferencesPath { get; set; }

        public void Change()
        {
            try
            {
                if (Project == null)
                {
                    return;
                }
                if (!string.IsNullOrWhiteSpace(Configuration))
                {
                    Project.SetProperty("Configuration", Configuration);
                    Project.Save();
                }
                if (PrebuildEvent)
                {
                    Project.SetProperty("PreBuildEvent", string.Empty);
                }
                if (PostbuildEvent)
                {
                    Project.SetProperty("PostBuildEvent", string.Empty);
                }
                if (!string.IsNullOrWhiteSpace(PlatformTarget))
                {
                    Project.SetProperty("PlatformTarget", PlatformTarget);
                }
                ProjectProperty outputPath = Project.GetProperty("OutputPath");
                if (outputPath == null || string.IsNullOrWhiteSpace(outputPath.EvaluatedValue))
                {
                    Project.SetProperty("OutputPath", "bin\\" + Configuration);
                }
                if (UpdateReferences)
                {
                    foreach (ProjectItem item in Project.GetItems("Reference"))
                    {
                        if (item == null)
                            continue;
                        ProjectMetadata hintPath = item.GetMetadata("HintPath");
                        if (hintPath != null && !string.IsNullOrWhiteSpace(hintPath.EvaluatedValue))
                        {
                            item.SetMetadataValue("HintPath",
                                Path.Combine(ReferencesPath, Path.GetFileName(hintPath.EvaluatedValue)));
                        }
                    }
                }
                Project.Save();
                Utility.Log(LogStatus.Ok, string.Format("File Updated - {0}", Project.FullPath), _log);
            }
            catch (Exception ex)
            {
                Utility.Log(LogStatus.Error, ex.Message, _log);
            }
        }
    }
}