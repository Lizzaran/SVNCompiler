using System;
using System.IO;
using Microsoft.Build.BuildEngine;
using Microsoft.Build.Evaluation;
using SVNCompiler.Data;
using Project = Microsoft.Build.Evaluation.Project;

namespace SVNCompiler.Class
{
    internal class Compiler
    {
        public static bool Compile(Project project, string logfile, Log log)
        {
            try
            {
                if (project != null)
                {
                    bool doLog = false;
                    if (!string.IsNullOrWhiteSpace(logfile))
                    {
                        string logDir = Path.GetDirectoryName(logfile);
                        if (!string.IsNullOrWhiteSpace(logDir))
                        {
                            doLog = true;
                            if (!Directory.Exists(logDir))
                            {
                                Directory.CreateDirectory(logDir);
                            }

                            var fileLogger = new FileLogger
                            {
                                Parameters = @"logfile=" + logfile,
                                ShowSummary = true
                            };
                            ProjectCollection.GlobalProjectCollection.RegisterLogger(fileLogger);
                        }
                    }
                    bool result = project.Build();
                    ProjectCollection.GlobalProjectCollection.UnregisterAllLoggers();
                    Utility.Log(result ? LogStatus.Ok : LogStatus.Error,
                        string.Format("Compile - {0}", project.FullPath), log);
                    if (!result && doLog && File.Exists(logfile))
                    {
                        string pathDir = Path.GetDirectoryName(logfile);
                        if (!string.IsNullOrWhiteSpace(pathDir))
                        {
                            File.Move(logfile, Path.Combine(pathDir, ("Error - " + Path.GetFileName(logfile))));
                        }
                    }
                    return result;
                }
            }
            catch (Exception ex)
            {
                Utility.Log(LogStatus.Error, ex.Message, log);
            }
            return false;
        }

        public static string GetOutputFilePath(Project project)
        {
            if (project != null)
            {
                string extension = project.GetPropertyValue("OutputType").ToLower() == "exe"
                    ? ".exe"
                    : (project.GetPropertyValue("OutputType").ToLower() == "library" ? ".dll" : string.Empty);
                string pathDir = Path.GetDirectoryName(project.FullPath);
                if (!string.IsNullOrWhiteSpace(extension) && !string.IsNullOrWhiteSpace(pathDir))
                {
                    return Path.Combine(pathDir, project.GetPropertyValue("OutputPath")) +
                           (project.GetPropertyValue("AssemblyName") + extension);
                }
            }
            return string.Empty;
        }
    }
}