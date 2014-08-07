using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Forms;
using System.Windows.Input;
using System.Windows.Media;
using MahApps.Metro.Controls;
using MahApps.Metro.Controls.Dialogs;
using Microsoft.Build.BuildEngine;
using Microsoft.Build.Evaluation;
using SharpSvn;
using Application = System.Windows.Application;
using Button = System.Windows.Controls.Button;
using ContextMenu = System.Windows.Controls.ContextMenu;
using DataGrid = System.Windows.Controls.DataGrid;
using MenuItem = System.Windows.Controls.MenuItem;
using Project = Microsoft.Build.Evaluation.Project;
using TextBox = System.Windows.Controls.TextBox;

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

namespace SVNCompiler.Views
{
    /// <summary>
    ///     Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : INotifyPropertyChanged
    {
        private readonly string BuildDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Builds");
        private readonly string LogDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Logs");
        private readonly string RepositoryDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Repositories");

        private Log _compileLog = new Log();
        private int _compileMaximum;
        private int _compileProgress;
        private Log _updateLog = new Log();
        private int _updateProgress;

        public MainWindow()
        {
            Utility.CreateFileFromResource("config.xml", "SVNCompiler.Resources.config.xml");
            Config = ((Config) Utility.MapXmlFileToClass(typeof (Config), "config.xml"));
            InitializeComponent();
            DataContext = this;
        }

        public Config Config { get; set; }

        public Log UpdateLog
        {
            get { return _updateLog; }
            set { _updateLog = value; }
        }

        public Log CompileLog
        {
            get { return _compileLog; }
            set { _compileLog = value; }
        }

        public int UpdateProgress
        {
            get { return _updateProgress; }
            set
            {
                _updateProgress = value;
                OnPropertyChanged("UpdateProgress");
            }
        }

        public int CompileProgress
        {
            get { return _compileProgress; }
            set
            {
                _compileProgress = value;
                OnPropertyChanged("CompileProgress");
            }
        }

        public int CompileMaximum
        {
            get { return _compileMaximum; }
            set
            {
                _compileMaximum = value;
                OnPropertyChanged("CompileMaximum");
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        private void MetroWindow_Closing(object sender, CancelEventArgs e)
        {
            Utility.MapClassToXmlFile(typeof (Config), Config, "config.xml");
        }

        private async void Credits_OnClick(object sender, RoutedEventArgs e)
        {
            await
                this.ShowMessageAsync("Appril",
                    "GitHub: Lizzaran" + Environment.NewLine + "IRC: irc.rizon.net#leaguesharp - Appril");
        }

        private void ReferencePath_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            var textBox = (TextBox) sender;
            if (textBox != null && string.IsNullOrWhiteSpace(textBox.SelectedText))
            {
                using (var folderDialog = new FolderBrowserDialog())
                {
                    if (folderDialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                    {
                        Config.Settings.References.NewPath = folderDialog.SelectedPath;
                    }
                }
            }
        }

        private void DataGrid_ContextMenuOpening(object sender, ContextMenuEventArgs e)
        {
            var dataGrid = (DataGrid) sender;
            if (dataGrid != null)
            {
                if (dataGrid.SelectedItems.Count == 0)
                {
                    e.Handled = true;
                }
                else if (dataGrid.CanUserAddRows)
                {
                    if (dataGrid.Items.IndexOf(dataGrid.CurrentItem) >= dataGrid.Items.Count - 1)
                    {
                        e.Handled = true;
                    }
                }
            }
            else
            {
                e.Handled = true;
            }
        }

        private void Delete_OnClick(object sender, RoutedEventArgs e)
        {
            var menuItem = (MenuItem) sender;
            if (menuItem != null)
            {
                var contextMenu = (ContextMenu) menuItem.Parent;
                if (contextMenu != null)
                {
                    var dataGrid = (DataGrid) contextMenu.PlacementTarget;
                    if (dataGrid != null)
                    {
                        IList rows = dataGrid.SelectedItems;
                        for (int i = rows.Count; i-- > 0;)
                        {
                            switch (dataGrid.Name)
                            {
                                case "RepositoriesDataGrid":
                                    string dir = Path.Combine(RepositoryDir,
                                        ((ConfigRepository) rows[i]).Url.GetHashCode().ToString("X"));
                                    if (Directory.Exists(dir))
                                    {
                                        Utility.ClearDirectory(dir);
                                        Directory.Delete(dir);
                                    }
                                    Config.Repositories.Remove((ConfigRepository) rows[i]);
                                    break;
                                case "ReferencesDataGrid":
                                    Config.References.Remove((ConfigReference) rows[i]);
                                    break;
                            }
                        }
                    }
                }
            }
        }

        private void UpdateButton_OnClick(object s, RoutedEventArgs e)
        {
            var bw = new BackgroundWorker();
            bw.DoWork += delegate
            {
                Application.Current.Dispatcher.Invoke(
                    () => OnProgressStart(UpdateButton, UpdateLog, () => UpdateProgress));
                if (Config.Repositories.Count == 0)
                {
                    Log(LogStatus.Info, "No Repositories", () => UpdateLog);
                }
                Parallel.ForEach(Config.Repositories, repository =>
                {
                    if (!repository.Enabled)
                    {
                        Log(LogStatus.Skipped, string.Format("Disabled - {0}", repository.Url), () => UpdateLog);
                    }
                    else if (string.IsNullOrWhiteSpace(repository.Url))
                    {
                        Log(LogStatus.Skipped, string.Format("No Url specified - {0}", repository.Url), () => UpdateLog);
                    }
                    else
                    {
                        try
                        {
                            string dir = Path.Combine(RepositoryDir, repository.Url.GetHashCode().ToString("X"));
                            using (var client = new SvnClient())
                            {
                                bool cleanUp = false;
                                client.Status(dir, new SvnStatusArgs {ThrowOnError = false},
                                    delegate(object sender, SvnStatusEventArgs args)
                                    {
                                        if (args.Wedged)
                                        {
                                            cleanUp = true;
                                        }
                                    });
                                if (cleanUp)
                                {
                                    client.CleanUp(dir);
                                }
                                client.CheckOut(new Uri(repository.Url), dir);
                                client.Update(dir);
                                Log(LogStatus.Ok, string.Format("Updated - {0}", repository.Url), () => UpdateLog);
                            }
                        }
                        catch (SvnException ex)
                        {
                            Log(LogStatus.Error, string.Format("{0} - {1}", ex.RootCause, repository.Url),
                                () => UpdateLog);
                        }
                        catch (Exception ex)
                        {
                            Log(LogStatus.Error, string.Format("{0} - {1}", ex.Message, repository.Url), () => UpdateLog);
                        }
                    }
                    Application.Current.Dispatcher.Invoke(() => UpdateProgress++);
                });
            };
            bw.RunWorkerCompleted += (sender, args) => OnProgressFinish(UpdateButton, "Update");
            bw.RunWorkerAsync();
        }

        private void CompileButton_OnClick(object s, RoutedEventArgs e)
        {
            string selectedConfiguration = ConfigurationsDropdown.SelectedItem == null
                ? Config.ProjectFile.Configurations.First()
                : ConfigurationsDropdown.SelectedItem.ToString();
            string selectedPlatform = PlatformsDropdown.SelectedItem == null
                ? Config.ProjectFile.Platforms.First()
                : PlatformsDropdown.SelectedItem.ToString();
            var bw = new BackgroundWorker();
            bw.DoWork += delegate
            {
                Application.Current.Dispatcher.Invoke(
                    () => OnProgressStart(CompileButton, CompileLog, () => CompileProgress));
                Utility.ClearDirectory(LogDir);
                Utility.ClearDirectory(BuildDir);
                if (Directory.Exists(RepositoryDir))
                {
                    string[] projectFiles = Directory.GetFiles(RepositoryDir, "*.csproj", SearchOption.AllDirectories);
                    if (!projectFiles.Any())
                    {
                        Log(LogStatus.Error, "No *.csproj files found", () => CompileLog);
                    }
                    Application.Current.Dispatcher.Invoke(() => CompileMaximum = projectFiles.Count());
                    foreach (string projectFile in projectFiles)
                    {
                        UpateProjectFile(projectFile, selectedConfiguration, selectedPlatform);
                        MoveCompiledFile(CompileProjectFile(projectFile));
                        Application.Current.Dispatcher.Invoke(() => CompileProgress++);
                    }
                }
                else
                {
                    Log(LogStatus.Error, "No *.csproj files found", () => CompileLog);
                }
            };
            bw.RunWorkerCompleted += (sender, args) =>
            {
                ProjectCollection.GlobalProjectCollection.UnloadAllProjects();
                OnProgressFinish(CompileButton, "Compile");
            };
            bw.RunWorkerAsync();
        }

        private void MoveCompiledFile(string path)
        {
            try
            {
                if (!string.IsNullOrWhiteSpace(path) && File.Exists(path))
                {
                    if (!Directory.Exists(BuildDir))
                    {
                        Directory.CreateDirectory(BuildDir);
                    }
                    string newPath = Path.Combine(BuildDir, Path.GetFileName(path));
                    File.Copy(path, newPath);
                    Log(File.Exists(newPath) ? LogStatus.Ok : LogStatus.Error,
                        string.Format("Move - {0} | {1}", path, newPath), () => CompileLog);
                }
            }
            catch (Exception ex)
            {
                Log(LogStatus.Error, ex.Message.ToString(CultureInfo.InvariantCulture), () => CompileLog);
            }
        }

        private string CompileProjectFile(string path)
        {
            try
            {
                ProjectCollection projectCollection = ProjectCollection.GlobalProjectCollection;
                ICollection<Project> searchProjects = projectCollection.GetLoadedProjects(path);
                Project project = searchProjects.Count == 0 ? null : searchProjects.First();
                if (project != null)
                {
                    if (!Directory.Exists(LogDir))
                    {
                        Directory.CreateDirectory(LogDir);
                    }
                    string logFile = Path.Combine(LogDir,
                        Utility.MakeValidFileName(Path.ChangeExtension(path, "txt")
                            .Remove(0,
                                RepositoryDir.Length)));
                    var fileLogger = new FileLogger
                    {
                        Parameters = @"logfile=" + logFile,
                        ShowSummary = true
                    };
                    projectCollection.RegisterLogger(fileLogger);
                    bool result = project.Build();
                    projectCollection.UnregisterAllLoggers();
                    Log(result ? LogStatus.Ok : LogStatus.Error, string.Format("Compile - {0}", path), () => CompileLog);
                    if (!result)
                    {
                        if (File.Exists(logFile))
                        {
                            File.Move(logFile,
                                Path.Combine(Path.GetDirectoryName(logFile), ("Error - " + Path.GetFileName(logFile))));
                        }
                    }
                    string extension = project.GetPropertyValue("OutputType") == "Exe"
                        ? ".exe"
                        : (project.GetPropertyValue("OutputType") == "Library" ? ".dll" : string.Empty);
                    return Path.Combine(Path.GetDirectoryName(path), project.GetPropertyValue("OutputPath")) +
                           (project.GetPropertyValue("AssemblyName") + extension);
                }
            }
            catch (Exception ex)
            {
                Log(LogStatus.Error, ex.Message.ToString(CultureInfo.InvariantCulture), () => CompileLog);
            }
            return string.Empty;
        }

        private void UpateProjectFile(string path, string selectedConfiguration, string selectedPlatform)
        {
            try
            {
                ProjectCollection projectCollection = ProjectCollection.GlobalProjectCollection;
                ICollection<Project> searchProjects = projectCollection.GetLoadedProjects(path);
                Project project = searchProjects.Count == 0 ? new Project(path) : searchProjects.First();
                ProjectProperty configuration = project.GetProperty("Configuration");
                if (configuration == null || configuration.EvaluatedValue != selectedConfiguration)
                {
                    project.SetProperty("Configuration", selectedConfiguration);
                    project.Save();
                }
                project.SetProperty("PlatformTarget", selectedPlatform);
                if (Config.Settings.References.Update)
                {
                    foreach (ProjectItem item in project.GetItems("Reference"))
                    {
                        if (item == null)
                            continue;
                        if (Config.References.Any(s => s.Name == item.EvaluatedInclude))
                        {
                            ProjectMetadata hintPath = item.GetMetadata("HintPath");
                            string fileName = hintPath != null
                                ? Path.GetFileName(hintPath.EvaluatedValue)
                                : string.Format("{0}.dll", item.EvaluatedInclude);
                            item.SetMetadataValue("HintPath", Path.Combine(Config.Settings.References.NewPath, fileName));
                        }
                    }
                    Log(LogStatus.Ok, string.Format("References Updated - {0}", path), () => CompileLog);
                }
                project.Save();
                Log(LogStatus.Ok, string.Format("File Updated - {0}", path), () => CompileLog);
            }
            catch (Exception ex)
            {
                Log(LogStatus.Error, ex.Message.ToString(CultureInfo.InvariantCulture), () => CompileLog);
            }
        }

        private void Log<T>(string status, string message, Expression<Func<T>> log)
        {
            var propertyInfo = ((MemberExpression) log.Body).Member as PropertyInfo;
            if (propertyInfo != null)
            {
                Application.Current.Dispatcher.Invoke(
                    () => ((Log) propertyInfo.GetValue(this, null)).Items.Add(new LogItem
                    {
                        Status = status,
                        Message = message
                    }));
            }
        }

        private void OnProgressStart<T>(Button button, Log log, Expression<Func<T>> progress)
        {
            foreach (TabItem tabItem in TabControl.Items)
            {
                tabItem.IsEnabled = false;
            }

            var propertyInfo = ((MemberExpression) progress.Body).Member as PropertyInfo;
            if (propertyInfo != null)
            {
                propertyInfo.SetValue(this, 0);
            }

            button.IsEnabled = false;
            button.Content = new ProgressRing {IsActive = true, Height = 15, Width = 15, Foreground = Brushes.White};
            log.Items.Clear();
        }

        private void OnProgressFinish(Button button, string content)
        {
            foreach (TabItem tabItem in TabControl.Items)
            {
                tabItem.IsEnabled = true;
            }
            button.IsEnabled = true;
            button.Content = content;
        }

        private void OnPropertyChanged(string propertyName)
        {
            if (PropertyChanged != null)
                PropertyChanged(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}