using System;
using System.IO;
using SharpSvn;
using SVNCompiler.Data;

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