using System;
using System.Collections.Generic;
using System.Collections;
using System.Diagnostics;
using System.Text;
using System.Xml;
using System.Threading;
using System.Threading.Tasks;

using Alphaleonis.Win32.Filesystem;

using System.Security.Cryptography.Utility;

using config = Synapse.Handlers.Legacy.RemoteCommand.Properties.Settings;

namespace Synapse.Handlers.Legacy.RemoteCommand
{
    public class PowershellWorkflow : Workflow
    {
        public PowershellWorkflow(WorkflowParameters wfp)
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

            List<String> servers = _wfp.Servers;
            if (_wfp.RunUsing == RunUsingProtocolType.ProcessStart)
            {
                servers = new List<string>();
                servers.Add("localhost");
            }

            // Build Command Array For Each Server
            remoteCommands = new List<RemoteCommand>();
            foreach (string server in servers)
            {
                String startWith = "-";
                String joinWith = " ";
                bool useQuotes = true;
                bool skipBlankValues = false;

                if (_wfp.Powershell.ArgType != null)
                {
                    if (_wfp.Powershell.ArgType.PrefixWith != null)
                        startWith = _wfp.Powershell.ArgType.PrefixWith;
                    if (_wfp.Powershell.ArgType.JoinWith != null)
                        joinWith = _wfp.Powershell.ArgType.JoinWith;
                    if (_wfp.Powershell.ArgType.UseQuotes != null)
                        useQuotes = Boolean.Parse(_wfp.Powershell.ArgType.UseQuotes);
                    if (_wfp.Powershell.ArgType.SkipBlankValues != null)
                        skipBlankValues = Boolean.Parse(_wfp.Powershell.ArgType.SkipBlankValues);

                    if (_wfp.Powershell.ArgType.value == ArgumentType.Ordered)
                        startWith = "";
                }

                // Build Argument String
                StringBuilder args = new StringBuilder();
                args.Append("-NonInteractive ");
                if (!string.IsNullOrWhiteSpace(_wfp.Powershell.Script)) 
                {
                    if (_wfp.Powershell.ExecutionPolicy != ExecutionPolicyType.None)
                    {
                        args.Append(@"-ExecutionPolicy " + _wfp.Powershell.ExecutionPolicy + " ");
                    }
                    else if (Path.IsPathRooted(_wfp.Powershell.Script))
                    {
                        // If Execution Policy isn't explicitly stated and the script is on a NAS, set the policy to "Bypass" as a default.
                        if (_wfp.Powershell.Script.StartsWith(@"\\"))
                        {
                            args.Append(@"-ExecutionPolicy Bypass ");
                        }
                    }
                    args.Append(@"-File """ + _wfp.Powershell.Script + @""" ");
                }
                else
                    args.Append(@"-Command """ + _wfp.Powershell.Command + @""" ");

                //if (_wfp.Parameters != null && !string.IsNullOrWhiteSpace(_wfp.Powershell.Script))
                if (_wfp.Parameters != null)
                    {
                    if (_wfp.Powershell.ArgType.value == ArgumentType.Named)
                    {
                        XmlNode node = (XmlNode)overrideXml[server];
                        args.Append(Utils.FormatNamedPowershellParameters(_wfp.Parameters, startWith, joinWith, useQuotes, skipBlankValues, node));
                    }
                    else if (_wfp.Powershell.ArgType.value == ArgumentType.Ordered)
                    {
                        XmlNode node = (XmlNode)overrideXml[server];
                        args.Append(Utils.FormatOrderedPowershellParameters(_wfp.Parameters, startWith, useQuotes, node));
                    }
                    else if (_wfp.Powershell.ArgType.value == ArgumentType.Xml)
                    {
                        String xml = _wfp.SerializeParameters();
                        args.Append(@"""" + xml.Replace(@"""", @"'") + @"""");
                    }
                }

                RemoteCommand cmd = new RemoteCommand();
                cmd.server = server;
                cmd.command = "powershell.exe";
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
