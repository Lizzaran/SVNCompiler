using System;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Windows.Controls;
using System.Windows.Data;
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
    public class PostbuildMoveValidationRule : ValidationRule
    {
        public override ValidationResult Validate(object value, CultureInfo cultureInfo)
        {
            var error = new StringBuilder();
            var bindingGroup = (BindingGroup) value;
            if (bindingGroup != null)
            {
                foreach (
                    ConfigSettingsPostbuildMove postMove in
                        from object item in bindingGroup.Items select item as ConfigSettingsPostbuildMove)
                {
                    if (postMove == null)
                    {
                        error.Append("The row can't be empty. ");
                        continue;
                    }
                    if (string.IsNullOrWhiteSpace(postMove.Wildcard))
                    {
                        error.Append("The wildcard should contain a value. ");
                    }
                    if (string.IsNullOrWhiteSpace(postMove.Directory) || !postMove.Directory.EndsWith("\\"))
                    {
                        error.Append("The directory should end with a backslash.");
                    }
                }
            }
            return !string.IsNullOrWhiteSpace(error.ToString())
                ? new ValidationResult(false, error.ToString())
                : ValidationResult.ValidResult;
        }
    }

    public class RepositoryValidationRule : ValidationRule
    {
        public override ValidationResult Validate(object value, CultureInfo cultureInfo)
        {
            var error = new StringBuilder();
            var bindingGroup = (BindingGroup) value;
            if (bindingGroup != null)
            {
                foreach (
                    ConfigRepository repository in
                        from object item in bindingGroup.Items select item as ConfigRepository)
                {
                    if (repository == null)
                    {
                        error.Append("The row can't be empty. ");
                        continue;
                    }
                    Uri uri;
                    if (!Uri.TryCreate(repository.Url, UriKind.RelativeOrAbsolute, out uri))
                    {
                        error.Append("The URL must be valid.");
                    }
                }
            }
            return !string.IsNullOrWhiteSpace(error.ToString())
                ? new ValidationResult(false, error.ToString())
                : ValidationResult.ValidResult;
        }
    }
}