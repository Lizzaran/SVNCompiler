using System;
using System.IO;
using SharpSvn;
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
    internal class SvnUpdater
    {
        public static void Update(ConfigRepository repository, Log log, string directory)
        {
            if (!repository.Enabled)
            {
                Utility.Log(LogStatus.Skipped, string.Format("Disabled - {0}", repository.Url), log);
            }
            else if (string.IsNullOrWhiteSpace(repository.Url))
            {
                Utility.Log(LogStatus.Skipped, string.Format("No Url specified - {0}", repository.Url), log);
            }
            else
            {
                try
                {
                    string dir = Path.Combine(directory, repository.Url.GetHashCode().ToString("X"));
                    using (var client = new SvnClient())
                    {
                        bool cleanUp = false;
                        client.Conflict +=
                            delegate(object sender, SvnConflictEventArgs eventArgs)
                            {
                                eventArgs.Choice = SvnAccept.TheirsFull;
                            };
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
                        Utility.Log(LogStatus.Ok, string.Format("Updated - {0}", repository.Url), log);
                    }
                }
                catch (SvnException ex)
                {
                    Utility.Log(LogStatus.Error, string.Format("{0} - {1}", ex.RootCause, repository.Url), log);
                }
                catch (Exception ex)
                {
                    Utility.Log(LogStatus.Error, string.Format("{0} - {1}", ex.Message, repository.Url), log);
                }
            }
        }
    }
}