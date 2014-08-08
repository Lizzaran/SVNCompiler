using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Build.Evaluation;
using SVNCompiler.Data;

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
                    ProjectProperty configuration = Project.GetProperty("Configuration");
                    if (configuration != null && configuration.EvaluatedValue != Configuration)
                    {
                        Project.SetProperty("Configuration", Configuration);
                        Project.Save();
                    }
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