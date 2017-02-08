using System;
using System.Collections.Generic;
using System.Collections;
using System.Diagnostics;
using System.Text;
using System.Xml;
using System.Threading;
using System.Threading.Tasks;

using Alphaleonis.Win32.Filesystem;

using Utilities.Encryption;

using config = Synapse.Handlers.Legacy.RemoteCommand.Properties.Settings;

namespace Synapse.Handlers.Legacy.RemoteCommand
{
    public class PlinkWorkflow : Workflow
    {
        public PlinkWorkflow(WorkflowParameters wfp)
            : base(wfp)
        {
        }

        public override void RunMainWorkflow()
        {
            remoteCommands = new List<RemoteCommand>();
            if (!string.IsNullOrWhiteSpace(_wfp.ParameterOverrideFile) && File.Exists(_wfp.ParameterOverrideFile))
            {
                try
                {
                    OnStepProgress("OverrideFile", "Parameter Override File Found. [" + _wfp.ParameterOverrideFile + "]");
                    XmlDocument doc = new XmlDocument();
                    doc.Load(_wfp.ParameterOverrideFile);
                    BuildPlinkCommands(doc.DocumentElement);

                }
                catch (Exception e)
                {
                    OnStepProgress("OverrideFile", e.Message);
                    BuildPlinkCommands();
                }
            }
            else
                BuildPlinkCommands();

            // Set Base Class Parameters
            RunScript(remoteCommands);

            if (!string.IsNullOrWhiteSpace(_wfp.ParameterOverrideFile) && File.Exists(_wfp.ParameterOverrideFile))
                File.Delete(_wfp.ParameterOverrideFile);
        }

        private void BuildPlinkCommands()
        {
            String startWith = "-";
            String joinWith = " ";
            bool useQuotes = true;
            bool skipBlankValues = false;

            if (_wfp.Plink.ArgType != null)
            {
                if (_wfp.Plink.ArgType.PrefixWith != null)
                    startWith = _wfp.Plink.ArgType.PrefixWith;
                if (_wfp.Plink.ArgType.JoinWith != null)
                    joinWith = _wfp.Plink.ArgType.JoinWith;
                if (_wfp.Plink.ArgType.UseQuotes != null)
                    useQuotes = Boolean.Parse(_wfp.Plink.ArgType.UseQuotes);
                if (_wfp.Plink.ArgType.SkipBlankValues != null)
                    skipBlankValues = Boolean.Parse(_wfp.Plink.ArgType.SkipBlankValues);

                if (_wfp.Plink.ArgType.value == ArgumentType.Ordered)
                    startWith = "";
            }

            foreach (UserType user in _wfp.Plink.Users)
            {
                // Build Argument String
                String scriptArgs = null;

                StringBuilder args = new StringBuilder();
                if (_wfp.Parameters != null)
                {
                    if (_wfp.Plink.ArgType.value == ArgumentType.Ordered)
                    {
                        scriptArgs = Utils.FormatOrderedParameters(_wfp.Parameters, startWith, useQuotes);
                    }
                    else if (_wfp.Plink.ArgType.value == ArgumentType.Named)
                    {
                        scriptArgs = Utils.FormatNamedParameters(_wfp.Parameters, startWith, joinWith, useQuotes, skipBlankValues);
                    }
                }

                if (_wfp.Plink.CommandFile != null)
                {
                    if ((!string.IsNullOrWhiteSpace(scriptArgs)) && (!string.IsNullOrWhiteSpace(_wfp.Plink.CommandFile.Name)))
                        scriptArgs = scriptArgs.Replace(@"""", @"\""");
                }

                if (config.Default.DebugMode)
                    args.Append(@"-v ");
                args.Append(@"-ssh ");


                if (!(String.IsNullOrWhiteSpace(user.Password)))
                {
                    String ptPassword = Utils.Decrypt(user.Password);
                    if (!(ptPassword.StartsWith("UNABLE TO DECRYPT")))
                        args.Append(@"-pw """ + ptPassword + @""" ");
                }
                else if (!string.IsNullOrWhiteSpace(_wfp.Plink.PrivateKey))
                    args.Append(@"-i """ + _wfp.Plink.PrivateKey + @""" ");
                
                if (!_wfp.Plink.RunInteractive)
                    args.Append(@"-batch ");

                if (!string.IsNullOrWhiteSpace(user.Name))
                    args.Append(user.Name + " ");

                if (!string.IsNullOrWhiteSpace(_wfp.Plink.Command))
                {
                    args.Append(_wfp.Plink.Command + " ");
                    if (scriptArgs != null)
                        args.Append(scriptArgs + " ");
                }
                else if (!string.IsNullOrWhiteSpace(_wfp.Plink.CommandFile.Name))
                {
                    string codePage = "437";
                    if (!string.IsNullOrWhiteSpace(_wfp.Plink.CommandFile.CodePage))
                        codePage = _wfp.Plink.CommandFile.CodePage;

                    args.Append(@"""dos2unix -" + codePage + " | sh /dev/stdin ");
                    if (scriptArgs != null)
                        args.Append(scriptArgs + " ");
                    args.Append(@""" < """ + _wfp.Plink.CommandFile.Name + @"""");

                }

                RemoteCommand cmd = new RemoteCommand();
                if (_wfp.RunUsing == RunUsingProtocolType.ProcessStart)
                    cmd.server = "localhost";
                else
                    cmd.server = _wfp.Servers[0];
                if (String.IsNullOrWhiteSpace(_wfp.WorkingDir))
                    cmd.command = "plink.exe";
                else
                    cmd.command = Path.Combine(_wfp.WorkingDir, "plink.exe");
                cmd.args = args.ToString().Trim();
                if (user.Name.IndexOf("@") >= 0)
                    cmd.callbackLabel = user.Name.Substring(user.Name.IndexOf("@") + 1);
                else
                    cmd.callbackLabel = user.Name;
                remoteCommands.Add(cmd);
            }
        }

        private void BuildPlinkCommands(XmlElement overrideParms)
        {
            OnStepProgress("OverrideFile", overrideParms.OuterXml);

            HashSet<string> validUsersOld = new HashSet<string>();

            Dictionary<string, UserType> validUsers = new Dictionary<string, UserType>();

            foreach (UserType user in _wfp.Plink.Users)
                validUsers.Add(user.Name.Trim(), user);

            String startWith = "-";
            String joinWith = " ";
            bool useQuotes = true;

            if (_wfp.Plink.ArgType != null)
            {
                if (_wfp.Plink.ArgType.value == ArgumentType.Ordered)
                    startWith = "";

                if (_wfp.Plink.ArgType.PrefixWith != null)
                    startWith = _wfp.Plink.ArgType.PrefixWith;
                if (_wfp.Plink.ArgType.JoinWith != null)
                    joinWith = _wfp.Plink.ArgType.JoinWith;
                if (_wfp.Plink.ArgType.UseQuotes != null)
                    useQuotes = Boolean.Parse(_wfp.Plink.ArgType.UseQuotes);
            }

            foreach (XmlNode userNode in overrideParms.ChildNodes)
            {
                string user = userNode.Attributes["value"].FirstChild.Value;
                UserType validUser = null;

                // if (!(validUsers.Contains(user.Trim())))
                if (!(validUsers.ContainsKey(user.Trim())))
                {
                    OnStepProgress("OverrideFile", "User [" + user.Trim() + "] Is Not A Valid User For This Package.");
                    continue;
                }
                else
                {
                    validUsers.TryGetValue(user.Trim(), out validUser);
                }

                // Build Argument String
                String scriptArgs = null;

                StringBuilder args = new StringBuilder();
                if (_wfp.Parameters != null)
                {
                    if (_wfp.Plink.ArgType.value == ArgumentType.Ordered)
                    {
                        scriptArgs = Utils.FormatOrderedParameters(_wfp.Parameters, startWith, useQuotes, userNode);
                    }
                    else if (_wfp.Plink.ArgType.value == ArgumentType.Named)
                    {
                        scriptArgs = Utils.FormatNamedParameters(_wfp.Parameters, startWith, joinWith, useQuotes);
                    }
                }

                if ((!string.IsNullOrWhiteSpace(scriptArgs)) && (!string.IsNullOrWhiteSpace(_wfp.Plink.CommandFile.Name)))
                    scriptArgs = scriptArgs.Replace(@"""", @"\""");

                args.Append(@"-ssh ");

                if (!(String.IsNullOrWhiteSpace(validUser.Password)))
                {
                    String ptPassword = Utils.Decrypt(validUser.Password);
                    if (!(ptPassword.StartsWith("UNABLE TO DECRYPT")))
                        args.Append(@"-pw """ + ptPassword + @""" ");
                }
                else if (!string.IsNullOrWhiteSpace(_wfp.Plink.PrivateKey))
                    args.Append(@"-i """ + _wfp.Plink.PrivateKey + @""" ");
                
                
                args.Append(@"-batch ");

                if (!string.IsNullOrWhiteSpace(user))
                    args.Append(user + " ");

                if (!string.IsNullOrWhiteSpace(_wfp.Plink.Command))
                {
                    args.Append(_wfp.Plink.Command + " ");
                    if (scriptArgs != null)
                        args.Append(scriptArgs + " ");
                }
                else if (!string.IsNullOrWhiteSpace(_wfp.Plink.CommandFile.Name))
                {
                    string codePage = "437";
                    if (!string.IsNullOrWhiteSpace(_wfp.Plink.CommandFile.CodePage))
                        codePage = _wfp.Plink.CommandFile.CodePage;

                    args.Append(@"""dos2unix -" + codePage + " | sh /dev/stdin ");
                    if (scriptArgs != null)
                        args.Append(scriptArgs + " ");
                    args.Append(@""" < """ + _wfp.Plink.CommandFile.Name + @"""");

                }

                RemoteCommand cmd = new RemoteCommand();
                if (_wfp.RunUsing == RunUsingProtocolType.ProcessStart)
                    cmd.server = "localhost";
                else
                    cmd.server = _wfp.Servers[0];
                cmd.command = "plink " + args.ToString().Trim();
                if (user.IndexOf("@") >= 0)
                    cmd.callbackLabel = user.Substring(user.IndexOf("@") + 1);
                else
                    cmd.callbackLabel = user;
                remoteCommands.Add(cmd);
            }
        }
    
    }

}
