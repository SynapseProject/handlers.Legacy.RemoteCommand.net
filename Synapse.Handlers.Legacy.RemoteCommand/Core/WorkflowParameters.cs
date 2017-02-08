using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Xml.Serialization;
using System.Xml;
using System.Text;

using Alphaleonis.Win32.Filesystem;

namespace Synapse.Handlers.Legacy.RemoteCommand
{
	[Serializable, XmlRoot( "RemoteCommand" )]
	public class WorkflowParameters
	{
        public WorkflowParameters() { }

        private List<String> _servers = new List<String>();
        private List<ExitCodeType> _validExitCodes = new List<ExitCodeType>();

        #region Public Global Workflow Parameters

        [XmlIgnore]
        public ScriptType CommandType;

        [XmlArrayItem(ElementName = "Server")]
        public List<String> Servers
        {
            get { return _servers; }
            set { _servers = value; }
        }

        [XmlElement]
        public ImpersonationType RunAs;

        [XmlElement]
        public RunUsingProtocolType RunUsing;

        [XmlElement]
        public string WorkingDir { get; set; }

        [XmlElement]
        public XmlElement Parameters;
        [XmlElement]
        public string ParameterOverrideFile { get; set; }

        [XmlElement("Timeout")]
        public TimeoutType Timeout { get; set; }

        [XmlArrayItem(ElementName = "Code")]
        public List<ExitCodeType> ValidExitCodes
        {
            get { return _validExitCodes; }
            set { _validExitCodes = value; }
        }



        #endregion

        #region Public CommandType Specific Workflow Parameters

        [XmlElement("Ant")]
        public AntCommandType Ant;
        [XmlElement("Powershell")]
        public PowershellCommandType Powershell;
        [XmlElement("Plink")]
        public PlinkCommandType Plink;
        [XmlElement("Pscp")]
        public PscpCommandType Pscp;

        #endregion

        #region Private Workflow Parameters

        [XmlIgnore]
        public string ScriptHandler { get; set; }
        [XmlIgnore]
        public string ScriptArguments { get; set; }

        #endregion

        #region Validation Flags

        [XmlIgnore]
        public bool IsValidServerCount { get; set; }
        [XmlIgnore]
        public bool IsValidCommandType { get; set; }
        [XmlIgnore]
        public bool IsValidPowershellAction { get; set; }
        [XmlIgnore]
        public bool IsValidPowershellCommandOrScript { get; set; }
        [XmlIgnore]
        public bool IsWorkingDirectoryValid { get; set; }
        [XmlIgnore]
        public bool IsSingleServerForPlink { get; set; }

        [XmlIgnore]
		public bool IsValid { get; protected set; }
        
        #endregion

        #region Public Workflow Parameter Methods

		public virtual void PrepareAndValidate() 
        {
            IsValidServerCount = true;
            IsValidCommandType = true;
            IsValidPowershellAction = true;
            IsValidPowershellCommandOrScript = true;
            IsWorkingDirectoryValid = true;
            IsSingleServerForPlink = true;
            IsValid = true;

            if (RunUsing == RunUsingProtocolType.ProcessStart && _servers.Count > 0)
                IsValidServerCount = false;
            else if (RunUsing != RunUsingProtocolType.ProcessStart &&_servers.Count <= 0)
                IsValidServerCount = false;

            if (!(string.IsNullOrWhiteSpace(WorkingDir)))
            {
                foreach (string server in _servers)
                {
                    string wdPath = Utils.GetServerLongPath(server, WorkingDir);
                    if ("localhost".Equals(server.ToLower()) || "127.0.0.1".Equals(server.ToLower()))
                        wdPath = WorkingDir;
                    IsWorkingDirectoryValid &= Utils.DirectoryExists(wdPath);
                }
            }

            if (CommandType == ScriptType.None)
                IsValidCommandType = false;

            if (CommandType == ScriptType.Powershell)
            {
                if (Powershell == null)
                    IsValidPowershellAction = false;
                else if (string.IsNullOrWhiteSpace(Powershell.Script) && string.IsNullOrWhiteSpace(Powershell.Command))
                    IsValidPowershellAction = false;
                else if (!string.IsNullOrWhiteSpace(Powershell.Script) && !string.IsNullOrWhiteSpace(Powershell.Command))
                    IsValidPowershellCommandOrScript = false;
            } 
            else if ((CommandType == ScriptType.Plink) || (CommandType == ScriptType.Pscp))
            {
                if (_servers.Count != 1 && RunUsing != RunUsingProtocolType.ProcessStart)
                    IsSingleServerForPlink = false;
            }

            IsValid = IsValidServerCount && IsValidCommandType && IsValidPowershellAction && IsValidPowershellCommandOrScript && IsWorkingDirectoryValid && IsSingleServerForPlink;
        }

		public virtual void Serialize(string filePath)
		{
			Utils.Serialize<WorkflowParameters>( this, true, filePath );
		}

        public virtual String Serialize(bool indented = true)
        {
            return Utils.Serialize<WorkflowParameters>(this, indented);
        }

        public static WorkflowParameters Deserialize(XmlElement el)
        {
            XmlSerializer s = new XmlSerializer(typeof(WorkflowParameters));
            return (WorkflowParameters)s.Deserialize(new System.IO.StringReader(el.OuterXml));
        }
        
        public static WorkflowParameters Deserialize(string filePath)
		{
			return Utils.DeserializeFile<WorkflowParameters>( filePath );
		}

        public String SerializeParameters()
        {
            String xml = Utils.Serialize<XmlElement>(this.Parameters, false);
            return xml.Substring(1);
        }

        public override String ToString()
        {
            StringBuilder sb = new StringBuilder();
            if (this.Ant != null)
                sb.Append(this.Ant.ToString());

            if (this.Powershell != null)
                sb.Append(this.Powershell.ToString());

            if (this.Plink != null)
                sb.Append(this.Plink.ToString());

            if (this.Pscp != null)
                sb.Append(this.Pscp.ToString());

            if (this.RunAs != null)
                sb.Append(this.RunAs.ToString());

            foreach (string server in Servers)
                sb.AppendLine(">> Server          : " + server);

            sb.AppendLine(">> WorkingDir      : " + this.WorkingDir);

            if (this.Timeout != null)
                sb.Append(this.Timeout.ToString());

            if (this._validExitCodes.Count > 0)
            {
                sb.AppendLine(">> ValidExitCodes");
                foreach (ExitCodeType code in _validExitCodes)
                    sb.Append(code.ToString());
            }

            if (this.Parameters != null)
            {
                sb.AppendLine(">> Parameters      : " + this.Parameters.Name);
                foreach (XmlNode node in this.Parameters.ChildNodes)
                    if (!(node.FirstChild == null) && !(string.IsNullOrWhiteSpace(node.FirstChild.Value)))
                        sb.AppendLine("   >> " + node.Name + " = " + node.FirstChild.Value);
                    else
                        sb.AppendLine("   >> " + node.Name + " = ");
            }

            return sb.ToString();
        }
        #endregion

        public WorkflowParameters FromXmlElement(XmlElement el)
        {
            XmlSerializer s = new XmlSerializer(typeof(WorkflowParameters));
            return (WorkflowParameters)s.Deserialize(new System.IO.StringReader(el.OuterXml));
        }
    }

    public class AntCommandType
    {
        [XmlElement]
        public string Target { get; set; }
        [XmlElement]
        public string Home { get; set; }
        [XmlElement]
        public string BuildFile { get; set; }

        public override String ToString()
        {
            StringBuilder sb = new StringBuilder();

            sb.AppendLine(">> Ant");
            sb.AppendLine("   >> AntHome      : " + this.Home);
            sb.AppendLine("   >> AntBuildfile : " + this.BuildFile);
            sb.AppendLine("   >> AntTarget    : " + this.Target);

            return sb.ToString();
        }
    }

    public class PowershellCommandType
    {
        [XmlElement]
        public string Script { get; set; }
        [XmlElement]
        public string Command { get; set; }
        [XmlElement]
        public ArgTypeType ArgType { get; set; }
        [XmlElement]
        public ExecutionPolicyType ExecutionPolicy { get; set; }

        public override String ToString()
        {
            StringBuilder sb = new StringBuilder();

            sb.AppendLine(">> Powershell");
            sb.AppendLine("   >> Script       : " + this.Script);
            sb.AppendLine("   >> Command      : " + this.Command);
            if (this.ArgType != null)
                sb.AppendLine(this.ArgType.ToString());
            if (!String.IsNullOrWhiteSpace(this.Script))
                sb.AppendLine("   >> ExePolicy    : " + this.ExecutionPolicy);

            return sb.ToString();
        }
    }

    public class PlinkCommandType
    {
        private List<UserType> _users = new List<UserType>();

        [XmlElement]
        public string PrivateKey { get; set; }
        [XmlArrayItem(ElementName = "User")]
        public List<UserType> Users
        {
            get { return _users; }
            set { _users = value; }
        }

        [XmlElement]
        public string Command { get; set; }
        [XmlElement]
        public CommandFileType CommandFile { get; set; }
        [XmlElement]
        public ArgTypeType ArgType { get; set; }
        [XmlElement]
        public bool RunInteractive { get; set; }

        public override String ToString()
        {
            StringBuilder sb = new StringBuilder();

            sb.AppendLine(">> Plink");
            sb.AppendLine("   >> PrivateKey   : " + this.PrivateKey);
            foreach (UserType user in Users)
                sb.AppendLine("   >> User         : " + user.Name);
            sb.AppendLine("   >> Command      : " + this.Command);
            if (this.CommandFile != null)
                sb.AppendLine(this.CommandFile.ToString());
            if (this.ArgType != null)
                sb.AppendLine(this.ArgType.ToString());
            
            return sb.ToString();
        }
    }

    public class PscpCommandType
    {
        private List<UserType> _users = new List<UserType>();
        private List<String> _sources = new List<String>();

        [XmlElement]
        public string PrivateKey { get; set; }
        [XmlArrayItem(ElementName = "User")]
        public List<UserType> Users
        {
            get { return _users; }
            set { _users = value; }
        }

        [XmlArrayItem(ElementName = "Source")]
        public List<String> Sources
        {
            get { return _sources; }
            set { _sources = value; }
        }
        [XmlElement]
        public string Destination { get; set; }
        [XmlElement]
        public string KeepFileAttributes { get; set; }
        [XmlIgnore]
        public bool _keepFileAttributes = false;


        public override String ToString()
        {
            StringBuilder sb = new StringBuilder();

            sb.AppendLine(">> Pscp");
            sb.AppendLine("   >> PrivateKey   : " + this.PrivateKey);
            foreach (UserType user in Users)
            {
                sb.AppendLine("   >> User         : " + user.Name);
                if (!(String.IsNullOrWhiteSpace(user.Password)))
                    sb.AppendLine("   >> Password     : " + @"********");
            }
            foreach (String source in this.Sources)
                sb.AppendLine("   >> Source       : " + source);
            sb.AppendLine("   >> Destination  : " + this.Destination);
            sb.AppendLine("   >> KeepFileAttrs: " + this.KeepFileAttributes);

            return sb.ToString();
        }

    }
    
    public class ImpersonationType
    {
        [XmlElement]
        public string User { get; set; }
        [XmlElement]
        public string Password { get; set; }
        [XmlAttribute]
        public RunAsProtocolType Protocol { get; set; }

        public override String ToString()
        {
            StringBuilder sb = new StringBuilder();

            sb.AppendLine(">> Impersonation");
            sb.AppendLine("   >> User         : " + this.User);
            sb.AppendLine("   >> Password     : " + "********");
            sb.AppendLine("   >> Protocol     : " + this.Protocol.ToString());

            return sb.ToString();
        }
    }

    public class UserType
    {
        [XmlAttribute]
        public string Password { get; set; }
        [XmlText]
        public string Name { get; set; }

        public override String ToString()
        {
            StringBuilder sb = new StringBuilder();

            sb.AppendLine("   >> UserName     : " + this.Name);
            sb.AppendLine("      >> Password  : " + @"********");

            return sb.ToString();
        }
    }

    public class TimeoutType
    {
        [XmlAttribute]
        public string KillProcess { get; set; }
        [XmlAttribute]
        public String ActionOnTimeout { get; set; }
        [XmlText]
        public long Value { get; set; }

        public override String ToString()
        {
            StringBuilder sb = new StringBuilder();

            sb.AppendLine(">> Timeout         : " + this.Value);
            sb.AppendLine("   >> KillProc     : " + this.KillProcess);
            sb.AppendLine("   >> TimeoutActn  : " + this.ActionOnTimeout);

            return sb.ToString();
        }
    }

    public class CommandFileType
    {
        [XmlAttribute("UseCodePage")]
        public string CodePage { get; set; }
        [XmlText]
        public string Name { get; set; }

        public override String ToString()
        {
            StringBuilder sb = new StringBuilder();

            sb.AppendLine("   >> CommandFile  : " + this.Name);
            sb.AppendLine("      >> CodePage  : " + this.CodePage);

            return sb.ToString();
        }
    }

    public class ArgTypeType
    {
        [XmlAttribute("PrefixWith")]
        public string PrefixWith { get; set; }
        [XmlAttribute("JoinWith")]
        public string JoinWith { get; set; }
        [XmlAttribute("UseQuotes")]
        public string UseQuotes { get; set; }
        [XmlAttribute("SkipBlankValues")]
        public string SkipBlankValues { get; set; }
        [XmlText]
        public ArgumentType value;

        public override String ToString()
        {
            StringBuilder sb = new StringBuilder();

            sb.AppendLine("   >> ArgType      : " + this.value);
            if (!String.IsNullOrWhiteSpace(this.PrefixWith))
                sb.AppendLine("      >> PrefixWith: " + this.PrefixWith);
            if (!String.IsNullOrWhiteSpace(this.JoinWith))
                sb.AppendLine("      >> JoinWith  : " + this.JoinWith);
            if (!String.IsNullOrWhiteSpace(this.UseQuotes))
                sb.AppendLine("      >> UseQuotes : " + this.UseQuotes);
            if (!String.IsNullOrWhiteSpace(this.SkipBlankValues))
                sb.AppendLine("      >> SkipBlanks: " + this.SkipBlankValues);

            return sb.ToString();
        }
    }

    public class ExitCodeType
    {
        [XmlAttribute("Operator")]
        public Operators Operator { get; set; }
        [XmlText]
        public string value;

        public override String ToString()
        {
            StringBuilder sb = new StringBuilder();

            sb.AppendLine("    >> Code        : " + this.value);
            sb.AppendLine("       >> Operator : " + this.Operator);

            return sb.ToString();
        }
    }
}