using System;
using System.Collections.Generic;
using System.Collections;
using System.Diagnostics;
using System.Text;
using System.Xml;
using System.Threading;
using System.Threading.Tasks;
using System.IO;

using Alphaleonis.Win32.Filesystem;

using Utilities.Encryption;

using config = Synapse.Handlers.Legacy.RemoteCommand.Properties.Settings;

namespace Synapse.Handlers.Legacy.RemoteCommand
{
    public class PscpWorkflow : Workflow
    {
        public PscpWorkflow(WorkflowParameters wfp)
            : base(wfp)
        {
        }

        public override void RunMainWorkflow()
        {
            remoteCommands = new List<RemoteCommand>();

            foreach (UserType user in _wfp.Pscp.Users)
            {
                foreach (string source in _wfp.Pscp.Sources)
                {
                    // Build Argument String
                    StringBuilder args = new StringBuilder();

                    if (!(String.IsNullOrWhiteSpace(user.Password)))
                    {
                        String ptPassword = Utils.Decrypt(user.Password);
                        if (!(ptPassword.StartsWith("UNABLE TO DECRYPT")))
                            args.Append(@"-pw """ + ptPassword + @""" ");
                    }
                    else if (!string.IsNullOrWhiteSpace(_wfp.Pscp.PrivateKey))
                        args.Append(@"-i """ + _wfp.Pscp.PrivateKey + @""" ");

                    args.Append(@"-batch ");

                    if (!(String.IsNullOrWhiteSpace(_wfp.Pscp.KeepFileAttributes)))
                    {
                        try { _wfp.Pscp._keepFileAttributes = Boolean.Parse(_wfp.Pscp.KeepFileAttributes); }
                        catch(Exception) {}
                    }
                    if (_wfp.Pscp._keepFileAttributes == true)
                        args.Append(@"-p ");

                    String srcStr = source;
                    if (srcStr.EndsWith(@"\"))
                        srcStr += @"\";

                    args.Append(@"-r """ + srcStr + @""" ");
                    args.Append(user.Name + @":" + _wfp.Pscp.Destination);

                    RemoteCommand cmd = new RemoteCommand();
                    if (_wfp.RunUsing == RunUsingProtocolType.ProcessStart)
                        cmd.server = "localhost";
                    else
                        cmd.server = _wfp.Servers[0];
                    if (String.IsNullOrWhiteSpace(_wfp.WorkingDir))
                        cmd.command = "pscp.exe";
                    else
                        cmd.command = System.IO.Path.Combine(_wfp.WorkingDir, "pscp.exe");
                    cmd.args = args.ToString().Trim();
                    if (user.Name.IndexOf("@") >= 0)
                        cmd.callbackLabel = user.Name.Substring(user.Name.IndexOf("@") + 1);
                    else
                        cmd.callbackLabel = user.Name;
                    remoteCommands.Add(cmd);

                }
            }

            // Set Base Class Parameters
            RunScript(remoteCommands);

        }
    }
}
