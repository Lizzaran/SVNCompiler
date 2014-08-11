using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Forms;
using System.Windows.Input;
using System.Windows.Media;
using MahApps.Metro.Controls;
using MahApps.Metro.Controls.Dialogs;
using Microsoft.Build.Evaluation;
using SVNCompiler.Class;
using SVNCompiler.Data;
using Application = System.Windows.Application;
using Button = System.Windows.Controls.Button;
using ContextMenu = System.Windows.Controls.ContextMenu;
using DataGrid = System.Windows.Controls.DataGrid;
using MenuItem = System.Windows.Controls.MenuItem;
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
        private readonly string _logDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Logs");
        private readonly string _repositoryDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Repositories");

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
            Title = string.Format("{0} {1}.{2}", Title, Assembly.GetExecutingAssembly().GetName().Version.Major,
                Assembly.GetExecutingAssembly().GetName().Version.Minor);
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

        private void MetroWindow_Loaded(object sender, RoutedEventArgs e)
        {
            if (Config.Settings.FirstRun)
            {
                Help_OnClick(null, null);
            }
        }

        private async void Credits_OnClick(object sender, RoutedEventArgs e)
        {
            await
                this.ShowMessageAsync("Appril",
                    "GitHub: Lizzaran" + Environment.NewLine + "IRC: irc.rizon.net#leaguesharp - Appril");
        }

        private async void Help_OnClick(object sender, RoutedEventArgs e)
        {
            await
                this.ShowMessageAsync("Help", Utility.ReadResourceString("SVNCompiler.Resources.help.txt"));
            Config.Settings.FirstRun = false;
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
                                    if (!string.IsNullOrWhiteSpace(((ConfigRepository) rows[i]).Url))
                                    {
                                        string dir = Path.Combine(_repositoryDir,
                                            ((ConfigRepository) rows[i]).Url.GetHashCode().ToString("X"));
                                        if (Directory.Exists(dir))
                                        {
                                            Utility.ClearDirectory(dir);
                                            Directory.Delete(dir);
                                        }
                                    }
                                    Config.Repositories.Remove((ConfigRepository) rows[i]);
                                    break;
                                case "PostbuildMoveDataGrid":
                                    Config.Settings.Postbuild.Move.Remove((ConfigSettingsPostbuildMove) rows[i]);
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
                    Utility.Log(LogStatus.Info, "No Repositories", UpdateLog);
                }
                Parallel.ForEach(Config.Repositories, repository =>
                {
                    SvnUpdater.Update(repository, UpdateLog, _repositoryDir);
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
                Utility.ClearDirectory(_logDir);
                if (Directory.Exists(_repositoryDir))
                {
                    List<string> projectFiles = GetProjectFiles();
                    if (!projectFiles.Any())
                    {
                        Utility.Log(LogStatus.Error, "No *.csproj files found", CompileLog);
                    }
                    Application.Current.Dispatcher.Invoke(() => CompileMaximum = projectFiles.Count());
                    foreach (string projectFile in projectFiles)
                    {
                        var pf = new ProjectFile(projectFile, CompileLog)
                        {
                            Configuration = selectedConfiguration,
                            PlatformTarget = selectedPlatform,
                            ReferencesPath = Config.Settings.References.NewPath,
                            UpdateReferences = Config.Settings.References.Update,
                            PostbuildEvent = true,
                            PrebuildEvent = true
                        };
                        pf.Change();
                        string logFile = Path.Combine(_logDir,
                            Utility.MakeValidFileName(Path.ChangeExtension(pf.Project.FullPath, "txt")
                                .Remove(0, _repositoryDir.Length)));
                        Compiler.Compile(pf.Project, logFile, CompileLog);
                        MoveCompiledFile(Compiler.GetOutputFilePath(pf.Project));
                        Application.Current.Dispatcher.Invoke(() => CompileProgress++);
                    }
                }
                else
                {
                    Utility.Log(LogStatus.Error, "No *.csproj files found", CompileLog);
                }
            };
            bw.RunWorkerCompleted += (sender, args) =>
            {
                ProjectCollection.GlobalProjectCollection.UnloadAllProjects();
                OnProgressFinish(CompileButton, "Compile");
            };
            bw.RunWorkerAsync();
        }

        private List<string> GetProjectFiles()
        {
            var projectFiles = new List<string>();
            foreach (string dir in from repository in Config.Repositories
                where !string.IsNullOrWhiteSpace(repository.Url)
                select Path.Combine(_repositoryDir, repository.Url.GetHashCode().ToString("X"), "trunk")
                into dir
                where Directory.Exists(dir)
                select dir)
            {
                projectFiles.AddRange(Directory.GetFiles(dir, "*.csproj", SearchOption.AllDirectories));
            }
            return projectFiles;
        }

        private void MoveCompiledFile(string path)
        {
            try
            {
                if (!string.IsNullOrWhiteSpace(path) && File.Exists(path))
                {
                    string fileName = Path.GetFileName(path);
                    string moveDirectory = MoveWildcardMatch(fileName);
                    if (!string.IsNullOrWhiteSpace(moveDirectory))
                    {
                        string newPath = moveDirectory + fileName;
                        Utility.Log(
                            (Config.Settings.Postbuild.Overwrite
                                ? Utility.OverwriteFile(path, newPath)
                                : Utility.RenameFileIfExists(path, newPath))
                                ? LogStatus.Ok
                                : LogStatus.Error, string.Format("Move - {0} | {1}", path, newPath), CompileLog);
                    }
                    else
                    {
                        Utility.Log(LogStatus.Info, string.Format("No Move-Wildcard found - {0}", path), CompileLog);
                    }
                }
            }
            catch (Exception ex)
            {
                Utility.Log(LogStatus.Error, ex.Message, CompileLog);
            }
        }

        private string MoveWildcardMatch(string fileName)
        {
            foreach (ConfigSettingsPostbuildMove wildcard in from wildcard in Config.Settings.Postbuild.Move
                let regex = new Regex(Utility.WildcardToRegex(wildcard.Wildcard))
                where regex.Match(fileName).Success
                select wildcard)
            {
                return wildcard.Directory;
            }
            return string.Empty;
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