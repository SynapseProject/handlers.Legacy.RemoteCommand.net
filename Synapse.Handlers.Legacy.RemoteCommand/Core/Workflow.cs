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

using Synapse.Core;

using config = Synapse.Handlers.Legacy.RemoteCommand.Properties.Settings;

namespace Synapse.Handlers.Legacy.RemoteCommand
{
	public class Workflow
	{
		protected WorkflowParameters _wfp = null;
        protected List<RemoteCommand> remoteCommands = new List<RemoteCommand>();

        //TODO : Make Function Pointers

//        public void OnLogMessage(string context, string message, LogLevel level = LogLevel.Info, Exception ex = null);
//        public bool OnProgress(string context, string message, StatusType status = StatusType.Running, long id = 0, int sequence = 0, bool cancel = false, Exception ex = null);

        public Action<string, string, LogLevel, Exception> OnLogMessage;
        public Func<string, string, StatusType, long, int, bool, Exception, bool> OnProgress;

        protected Workflow(WorkflowParameters wfp)
		{
			_wfp = wfp;
		}

        public Workflow() { }

        public static Workflow GetInstance(WorkflowParameters wfp)
        {
            Workflow wf = null;
            wfp.CommandType = GetCommandType(wfp);

            switch (wfp.CommandType)
            {
                case ScriptType.Ant:
                    wf = new AntWorkflow(wfp);
                    break;
                case ScriptType.Powershell:
                    wf = new PowershellWorkflow(wfp);
                    break;
                case ScriptType.Plink:
                    wf = new PlinkWorkflow(wfp);
                    break;
                case ScriptType.Pscp :
                    wf = new PscpWorkflow(wfp);
                    break;
                default:
                    wf = new Workflow(wfp);
                    break;
            }

            return wf;

        }
        
        protected static ScriptType GetCommandType(WorkflowParameters parms)
        {
            ScriptType sType = ScriptType.None;
            bool singleScriptType = true;

            // Determine ScriptType By ComplexType Elements In Workflow Parameters
            if (parms.Ant != null)
                if (sType == ScriptType.None)
                    sType = ScriptType.Ant;
                else
                    singleScriptType = false;

            if (parms.Powershell != null)
                if (sType == ScriptType.None)
                    sType = ScriptType.Powershell;
                else
                    singleScriptType = false;

            if (parms.Plink != null)
                if (sType == ScriptType.None)
                    sType = ScriptType.Plink;
                else
                    singleScriptType = false;

            if (parms.Pscp != null)
                if (sType == ScriptType.None)
                    sType = ScriptType.Pscp;
                else
                    singleScriptType = false;

            // Multiple ComplexType Elements Found, Unable To Determine Script Type
            if (singleScriptType == false)
                sType = ScriptType.None;

            return sType;
        }

		public WorkflowParameters Parameters { get { return _wfp; } set { _wfp = value as WorkflowParameters; } }

		public void ExecuteAction()
		{
			string context = "ExecuteAction";

			string msg = Utils.GetHeaderMessage( string.Format( "Entering Main Workflow.") );
			if( OnStepStarting( context, msg ) )
			{
				return;
			}

            OnStepProgress(context, _wfp.Serialize(false));
            Stopwatch clock = new Stopwatch();
            clock.Start();

            Exception ex = null;
            try
            {
                bool isValid = ValidateParameters();

                if (isValid)
                {
                    RunMainWorkflow();
                }
                else
                {
                    OnStepProgress(context, "Package Validation Failed");
                    throw new Exception("Package Validation Failed");
                }
            }
            catch (Exception exception)
            {
                ex = exception;
            }

            bool ok = ex == null;
            msg = Utils.GetHeaderMessage(string.Format("End Main Workflow: {0}, Total Execution Time: {1}",
                ok ? "Complete." : "One or more steps failed.", clock.ElapsedSeconds()));
//            OnStepFinished(context, msg, ok ? PackageStatus.Complete : PackageStatus.Failed, int.MaxValue, 0, ex);
            OnProgress(context, msg, ok ? StatusType.Complete : StatusType.Failed, 0, int.MaxValue, false, ex);
        }

        public virtual void RunMainWorkflow()
        {
            OnStepProgress("RunMainWorkflow", string.Format("Unknown CommandType : {0}", _wfp.CommandType));
        }

        bool ValidateParameters()
        {
            string context = "Validate";
            const int padding = 50;

            OnStepProgress(context, Utils.GetHeaderMessage("Begin [PrepareAndValidate]"));
            OnStepProgress(context, Utils.GetMessagePadRight("Workflow Type", this.GetType().Name, padding));

            _wfp.PrepareAndValidate();

            OnStepProgress(context, Utils.GetMessagePadRight("ServerCount", _wfp.IsValidServerCount, padding));
            OnStepProgress(context, Utils.GetMessagePadRight("WorkingDirExists", _wfp.IsWorkingDirectoryValid, padding));
            OnStepProgress(context, Utils.GetMessagePadRight("ValidCommandType", _wfp.IsValidCommandType, padding));
            if (_wfp.CommandType == ScriptType.Powershell)
            {
                OnStepProgress(context, Utils.GetMessagePadRight("PS Action", _wfp.IsValidPowershellAction, padding));
                OnStepProgress(context, Utils.GetMessagePadRight("PS Cmd or Script", _wfp.IsValidPowershellCommandOrScript, padding));
            }

            OnStepProgress(context, Utils.GetMessagePadRight("WorkflowParameters.IsValid", _wfp.IsValid, padding));
            OnStepProgress(context, Utils.GetHeaderMessage("End [PrepareAndValidate]"));

            return _wfp.IsValid;
        }

        protected string BuildRunAsCommand(String server, String command, RunAsProtocolType protocol)
        {
            StringBuilder sb = new StringBuilder();
            string user = config.Default.DefaultRunAsUser;
            string pass = config.Default.DefaultRunAsPass;

            if (_wfp.RunAs != null)
            {
                if (_wfp.RunAs.User != null)
                    user = _wfp.RunAs.User;

                if (_wfp.RunAs.Password != null)
                    pass = _wfp.RunAs.Password;
            }
            Cipher cipher = new Cipher(config.Default.PassPhrase, config.Default.SaltValue, config.Default.InitVector);
            pass = cipher.Decrypt(pass);
            if (pass.StartsWith("UNABLE TO DECRYPT - Error"))
            {
                OnStepProgress("BuildRunAsCommand", pass);
                return command;
            }

            if (protocol == RunAsProtocolType.WinRm)
            {
                sb.Append(@" -u:" + user);
                sb.Append(@" -p:" + pass);
                sb.Append(@" -r:" + server);
                if (!String.IsNullOrWhiteSpace(_wfp.WorkingDir))
                    sb.Append(@" -d:""" + _wfp.WorkingDir + @"""");
                sb.Append(@" -ad");
                sb.Append(@" " + command);
            }
            else
                sb.Append(command);

            return sb.ToString();
        }

        public virtual void RunScript(List<RemoteCommand> commands)
        {
            if (_wfp.RunAs != null && _wfp.RunUsing == RunUsingProtocolType.WMI)
            {
                foreach (RemoteCommand command in commands)
                {
                    command.args =
                        BuildRunAsCommand(_wfp.Servers[0], command.command + " " + command.args, _wfp.RunAs.Protocol);
                    command.command = "winrs";
                }
            }

            long timeoutValue = 0;
            bool killProcess = false;
            TimeoutAction actionOnTimeout = TimeoutAction.Continue;

            if (_wfp.Timeout != null)
            {
                timeoutValue = _wfp.Timeout.Value;
                if (!(String.IsNullOrWhiteSpace(_wfp.Timeout.KillProcess)))
                    try { killProcess = Boolean.Parse(_wfp.Timeout.KillProcess); }
                    catch (Exception) { }

                if (killProcess)
                    actionOnTimeout = TimeoutAction.Error;      // Set Default Action When Kill Specified to "Error"

                if (!(String.IsNullOrWhiteSpace(_wfp.Timeout.ActionOnTimeout)))
                    try { actionOnTimeout = ((TimeoutAction)Enum.Parse(typeof(TimeoutAction), _wfp.Timeout.ActionOnTimeout)); }
                    catch (Exception) { }
            }

            bool errorOccured = false;
            if (_wfp.RunUsing == RunUsingProtocolType.WMI)
            {
                Parallel.ForEach(commands, command =>
                    {

                        Int32 exitCode = WMIUtil.RunCommand(command.command + " " + command.args, command.server, _wfp.WorkingDir, timeoutValue, killProcess, actionOnTimeout, OnStepProgress, command.callbackLabel);

                        if (!(IsValidExitCode(exitCode)))
                        {
                            OnStepProgress(command.server, "ERROR - Invalid Exit Code [" + exitCode + "] Was Returned.");
                            errorOccured = true;
                        }
                    }
                );
            }
            else if (_wfp.RunUsing == RunUsingProtocolType.ProcessStart)
            {
                Parallel.ForEach(commands, command =>
                    {
                        Int32 exitCode = LocalProcessUtil.RunCommand(command.command, command.args, _wfp.WorkingDir, timeoutValue, actionOnTimeout, OnStepProgress, command.callbackLabel);

                        if (!(IsValidExitCode(exitCode)))
                        {
                            OnStepProgress(command.server, "ERROR - Invalid Exit Code [" + exitCode + "] Was Returned.");
                            errorOccured = true;
                        }
                    }
                );
            }
            else
            {
                throw new Exception("Protocol [" + _wfp.RunUsing + "] Is Not Yet Implemented.");
            }

            if (errorOccured)
                throw new Exception("Invalid Exit Codes Returned.");
        }

        public bool IsValidExitCode(Int32 exitCode)
        {
            bool isValid = false;

            if (_wfp.ValidExitCodes == null)
                return true;

            if (_wfp.ValidExitCodes.Count == 0)
                return true;

            foreach (ExitCodeType validCode in _wfp.ValidExitCodes)
            {
                try
                {
                    switch (validCode.Operator)
                    {
                        case Operators.EqualTo:
                            isValid = (exitCode == Int32.Parse(validCode.value));
                            break;
                        case Operators.NotEqualTo:
                            isValid = (exitCode != Int32.Parse(validCode.value));
                            break;
                        case Operators.LessThan:
                            isValid = (exitCode < Int32.Parse(validCode.value));
                            break;
                        case Operators.LessThanOrEqualTo:
                            isValid = (exitCode <= Int32.Parse(validCode.value));
                            break;
                        case Operators.GreaterThan:
                            isValid = (exitCode > Int32.Parse(validCode.value));
                            break;
                        case Operators.GreaterThanOrEqualTo:
                            isValid = (exitCode >= Int32.Parse(validCode.value));
                            break;
                        case Operators.Between:
                            Int32[] vals = ParseBetweenValues(validCode.value);
                            isValid = (exitCode >= vals[0] && exitCode <= vals[1]);
                            break;
                        case Operators.NotBetween:
                            Int32[] vals1 = ParseBetweenValues(validCode.value);
                            isValid = (exitCode < vals1[0] || exitCode > vals1[1]);
                            break;
                        default :
                            OnStepProgress("IsValidExitCode", "Unknown Operator Type [" + validCode.Operator + "] Has Been Ignored.");
                            break;
                    }
                }
                catch (Exception)
                {

                }

                if (isValid)
                    break;
            }

            return isValid;
        }

        private Int32[] ParseBetweenValues(String values)
        {
            Int32[] rVals = new Int32[2];

            String[] sVals = values.Split(',');
            if (sVals.Length < 2)
                throw new Exception("Invalid Values Specified For Between Function");

            Int32 i1 = Int32.Parse(sVals[0]);
            Int32 i2 = Int32.Parse(sVals[1]);

            if (i1 < i2)
            {
                rVals[0] = i1;
                rVals[1] = i2;
            }
            else
            {
                rVals[0] = i2;
                rVals[1] = i1;
            }

            return rVals;
        }

        #region NotifyProgress Events
		int _cheapSequence = 0;

//		void p_StepProgress(object sender, AdapterProgressEventArgs e)
//		{
//			OnStepProgress( e.Context, e.Message, PackageStatus.Running, _cheapSequence++, 0, e.Exception );
//		}

		/// <summary>
		/// Notify of step beginning. If return value is True, then cancel operation.
		/// Defaults: PackageStatus.Running, Id = _cheapSequence++, Severity = 0, Exception = null.
		/// </summary>
		/// <param name="context">The method name.</param>
		/// <param name="message">Descriptive message.</param>
		/// <returns>AdapterProgressCancelEventArgs.Cancel value.</returns>
		bool OnStepStarting(string context, string message)
		{
            OnProgress(context, message, StatusType.Running, 0, _cheapSequence++, false, null);
			return false;
		}

		/// <summary>
		/// Notify of step progress.
		/// Defaults: PackageStatus.Running, Id = _cheapSequence++, Severity = 0, Exception = null.
		/// </summary>
		/// <param name="context">The method name.</param>
		/// <param name="message">Descriptive message.</param>
		protected void OnStepProgress(string context, string message)
		{
            OnProgress(context, message, StatusType.Running, 0, _cheapSequence++, false, null);
		}

		/// <summary>
		/// Notify of step completion.
		/// Defaults: PackageStatus.Running, Id = _cheapSequence++, Severity = 0, Exception = null.
		/// </summary>
		/// <param name="context">The method name or workflow activty.</param>
		/// <param name="message">Descriptive message.</param>
		protected void OnStepFinished(string context, string message)
		{
            OnProgress(context, message, StatusType.Running, 0, _cheapSequence++, false, null);
        }
        #endregion

    }

    public class RemoteCommand
    {
        public string server { get; set; }
        public string command { get; set; }
        public string args { get; set; }
        public string callbackLabel { get; set; }
    }
}