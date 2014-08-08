using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Xml.Serialization;

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

namespace SVNCompiler
{
    public class ConfigProjectFile
    {
        public string[] Configurations
        {
            get
            {
                return new[]
                {
                    "Release", "Debug"
                };
            }
        }

        public string[] Platforms
        {
            get
            {
                return new[]
                {
                    "x86", "x64", "AnyCpu"
                };
            }
        }
    }

    [XmlType(AnonymousType = true)]
    [XmlRoot(Namespace = "", IsNullable = false)]
    public class Config : INotifyPropertyChanged
    {
        private ObservableCollection<ConfigRepository> _repositories;
        private ConfigSettings _settings;

        public ConfigSettings Settings
        {
            get { return _settings; }
            set
            {
                _settings = value;
                OnPropertyChanged("Settings");
            }
        }

        public ConfigProjectFile ProjectFile
        {
            get { return new ConfigProjectFile(); }
        }

        [XmlArrayItem("Repository", IsNullable = false)]
        public ObservableCollection<ConfigRepository> Repositories
        {
            get { return _repositories; }
            set
            {
                _repositories = value;
                OnPropertyChanged("Repositories");
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        private void OnPropertyChanged(string propertyName)
        {
            if (PropertyChanged != null)
                PropertyChanged(this, new PropertyChangedEventArgs(propertyName));
        }
    }


    [XmlType(AnonymousType = true)]
    public class ConfigSettings : INotifyPropertyChanged
    {
        private ConfigSettingsReferences _references;

        public ConfigSettingsReferences References
        {
            get { return _references; }
            set
            {
                _references = value;
                OnPropertyChanged("References");
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        private void OnPropertyChanged(string propertyName)
        {
            if (PropertyChanged != null)
                PropertyChanged(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    [XmlType(AnonymousType = true)]
    public class ConfigSettingsReferences : INotifyPropertyChanged
    {
        private string _newPath;
        private bool _update;

        public bool Update
        {
            get { return _update; }
            set
            {
                _update = value;
                OnPropertyChanged("Update");
            }
        }

        public string NewPath
        {
            get { return _newPath; }
            set
            {
                _newPath = value;
                OnPropertyChanged("NewPath");
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        private void OnPropertyChanged(string propertyName)
        {
            if (PropertyChanged != null)
                PropertyChanged(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    [XmlType(AnonymousType = true)]
    public class ConfigRepository : INotifyPropertyChanged
    {
        private string _author;
        private bool _enabled;
        private string _info;
        private string _url;

        public string Info
        {
            get { return _info; }
            set
            {
                _info = value;
                OnPropertyChanged("Info");
            }
        }

        public string Author
        {
            get { return _author; }
            set
            {
                _author = value;
                OnPropertyChanged("Author");
            }
        }

        public string Url
        {
            get { return _url; }
            set
            {
                _url = value;
                OnPropertyChanged("Url");
            }
        }

        [XmlAttribute]
        public bool Enabled
        {
            get { return _enabled; }
            set
            {
                _enabled = value;
                OnPropertyChanged("Enabled");
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        private void OnPropertyChanged(string propertyName)
        {
            if (PropertyChanged != null)
                PropertyChanged(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}