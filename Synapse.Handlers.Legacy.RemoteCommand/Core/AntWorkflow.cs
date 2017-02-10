using System;
using System.Collections.Generic;
using System.Collections;
using System.Diagnostics;
using System.Text;
using System.Xml;
using System.Threading;
using System.Threading.Tasks;

using Alphaleonis.Win32.Filesystem;

namespace Synapse.Handlers.Legacy.RemoteCommand
{
    public class AntWorkflow : Workflow
    {
        public AntWorkflow(WorkflowParameters wfp)
            : base(wfp)
        {
        }


        public override void RunMainWorkflow(bool isDryRun)
        {
            XmlNode overrideParameters = null;
            Hashtable overrideXml = new Hashtable();

            // Check For Parameter Override File, Build Override Hashtable If Found
            if (!string.IsNullOrWhiteSpace(_wfp.ParameterOverrideFile) && File.Exists(_wfp.ParameterOverrideFile))
            {
                try
                {
                    OnStepProgress("OverrideFile", "Parameter Override File Found. [" + _wfp.ParameterOverrideFile + "]");
                    XmlDocument doc = new XmlDocument();
                    doc.Load(_wfp.ParameterOverrideFile);
                    overrideParameters = doc.DocumentElement;
                    OnStepProgress("OverrideFile", overrideParameters.OuterXml);
                    foreach (XmlNode node in overrideParameters.ChildNodes)
                    {
                        string key = node.Attributes["name"].Value;
                        overrideXml.Add(key, node);
                    }
                }
                catch (Exception e)
                {
                    OnStepProgress("OverrideFile", e.Message);
                    overrideXml.Clear();
                }
            }


            remoteCommands = new List<RemoteCommand>();
            List<string> servers = _wfp.Servers;
            if (_wfp.RunUsing == RunUsingProtocolType.ProcessStart)
            {
                servers = new List<string>();
                servers.Add("localhost");
            }

            foreach (string server in servers)
            {
                // Build Argument String
                StringBuilder args = new StringBuilder();
                if (!string.IsNullOrWhiteSpace(_wfp.Ant.BuildFile))
                    args.Append(@"-buildfile " + _wfp.Ant.BuildFile + " ");

                if (_wfp.Parameters != null)
                {
                    XmlNode node = (XmlNode)overrideXml[server];
                    args.Append(Utils.FormatNamedParameters(_wfp.Parameters, "-D", "=", true, false, node));
                }

                if (!string.IsNullOrWhiteSpace(_wfp.Ant.Target))
                    args.Append(_wfp.Ant.Target + " ");

                string command = String.Empty;
                if (!string.IsNullOrWhiteSpace(_wfp.Ant.Home))
                    command = _wfp.Ant.Home + @"\ant";
                else
                    command = "ant";

                RemoteCommand cmd = new RemoteCommand();
                cmd.server = server;
                cmd.command = command;
                cmd.args = args.ToString().Trim();
                cmd.callbackLabel = server;
                remoteCommands.Add(cmd);
            }

            RunScript(remoteCommands, isDryRun);

            if (!string.IsNullOrWhiteSpace(_wfp.ParameterOverrideFile) && File.Exists(_wfp.ParameterOverrideFile))
            {
                File.Delete(_wfp.ParameterOverrideFile);
            }
        }

    }
}
