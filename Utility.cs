using System;
using System.IO;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
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
    internal class Utility
    {
        public static void MapClassToXmlFile(Type type, object obj, string path)
        {
            var serializer = new XmlSerializer(type);
            using (var sw = new StreamWriter(path, false, Encoding.UTF8))
            {
                serializer.Serialize(sw, obj);
            }
        }

        public static object MapXmlFileToClass(Type type, string path)
        {
            var serializer = new XmlSerializer(type);
            using (var reader = new StreamReader(path, Encoding.UTF8))
            {
                return serializer.Deserialize(reader);
            }
        }

        public static void CreateFileFromResource(string path, string resource, bool overwrite = false)
        {
            if (!overwrite && File.Exists(path))
                return;
            using (Stream stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(resource))
            {
                if (stream != null)
                {
                    using (var reader = new StreamReader(stream))
                    {
                        using (var sw = new StreamWriter(path, false, Encoding.UTF8))
                        {
                            sw.Write(reader.ReadToEnd());
                        }
                    }
                }
            }
        }

        public static void ClearDirectory(string directory)
        {
            try
            {
                var dir = new DirectoryInfo(directory);
                foreach (FileInfo fi in dir.GetFiles())
                {
                    fi.Delete();
                }
                foreach (DirectoryInfo di in dir.GetDirectories())
                {
                    ClearDirectory(di.FullName);
                    di.Delete();
                }
            }
            catch (Exception ex)
            {
            }
        }

        public static string MakeValidFileName(string name)
        {
            string invalidChars = Regex.Escape(new string(Path.GetInvalidFileNameChars()));
            string invalidRegStr = string.Format(@"([{0}]*\.+$)|([{0}]+)", invalidChars);
            return Regex.Replace(name, invalidRegStr, "_");
        }
    }
}