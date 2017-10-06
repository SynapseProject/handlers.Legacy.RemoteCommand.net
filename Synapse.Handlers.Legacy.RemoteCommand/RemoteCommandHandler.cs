using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using System.Xml;
using System.Xml.Serialization;
using System.IO;

using Synapse.Handlers.Legacy.RemoteCommand;

using Synapse.Core;

public class RemoteCommandHandler : HandlerRuntimeBase
{
    int seqNo = 0;
    override public ExecuteResult Execute(HandlerStartInfo startInfo)
    {
        XmlSerializer ser = new XmlSerializer(typeof(WorkflowParameters));
        WorkflowParameters wfp = new WorkflowParameters();
        TextReader reader = new StringReader(startInfo.Parameters);
        wfp = (WorkflowParameters)ser.Deserialize(reader);

        Workflow wf = Workflow.GetInstance(wfp);

        wf.OnLogMessage = this.OnLogMessage;
        wf.OnProgress = this.OnProgress;

        seqNo = 0;
        OnProgress("Execute", "Starting", StatusType.Running, startInfo.InstanceId, seqNo++);
        wf.ExecuteAction(startInfo);

        return new ExecuteResult() { Status = StatusType.Complete };
    }

    public override object GetConfigInstance()
    {
        return null;
    }

    public override object GetParametersInstance()
    {
        WorkflowParameters wfp = new WorkflowParameters();

        wfp.Servers = new List<string>();
        wfp.Servers.Add( "localhost" );
        wfp.Servers.Add( "server001" );

        wfp.RunAs = new ImpersonationType();
        wfp.RunAs.User = "MyUsername";
        wfp.RunAs.Password = "MyPassword";
        wfp.RunAs.Protocol = RunAsProtocolType.WinRm;

        wfp.RunUsing = RunUsingProtocolType.ProcessStart;
        wfp.WorkingDir = @"C:\Temp";
        wfp.Timeout = new TimeoutType();
        wfp.Timeout.Value = 60000;
        wfp.Timeout.ActionOnTimeout = "Error";
        wfp.Timeout.KillProcess = "true";

        wfp.ValidExitCodes = new List<ExitCodeType>();
        ExitCodeType code = new ExitCodeType();
        code.Operator = Operators.NotEqualTo;
        code.value = "0";

        wfp.Ant = new AntCommandType();
        wfp.Ant.Home = @"C:\ant";
        wfp.Ant.BuildFile = @"C:\Temp\build.xml";
        wfp.Ant.Target = "MyAntTarget";

        wfp.Powershell = new PowershellCommandType();
        wfp.Powershell.ArgType = new ArgTypeType();
        wfp.Powershell.ArgType.PrefixWith = "-";
        wfp.Powershell.ArgType.UseQuotes = "true";
        wfp.Powershell.ArgType.JoinWith = ", ";
        wfp.Powershell.Command = "hostname";
        wfp.Powershell.ExecutionPolicy = ExecutionPolicyType.Bypass;
        wfp.Powershell.Script = @"C:\Script\MyScript.ps1";

        wfp.Plink = new PlinkCommandType();
        wfp.Plink.ArgType = new ArgTypeType();
        wfp.Plink.ArgType.SkipBlankValues = "true";
        wfp.Plink.ArgType.JoinWith = " ";
        wfp.Plink.ArgType.PrefixWith = "";
        wfp.Plink.ArgType.UseQuotes = "false";
        wfp.Plink.Command = "ls";
        wfp.Plink.CommandFile = new CommandFileType();
        wfp.Plink.CommandFile.Name = @"MyCommandFile.sh";
        wfp.Plink.CommandFile.CodePage = "";
        wfp.Plink.PrivateKey = "MyKey.ppk";
        wfp.Plink.RunInteractive = true;
        wfp.Plink.Users = new List<UserType>();
        UserType user = new UserType();
        user.Name = "user@server";
        user.Password = "MyPassword";
        wfp.Plink.Users.Add( user );

        wfp.Pscp = new PscpCommandType();
        wfp.Pscp.Destination = @"\Users\user\temp";
        wfp.Pscp.KeepFileAttributes = "true";
        wfp.Pscp.PrivateKey = "MyKey.ppk";
        wfp.Pscp.Sources = new List<string>();
        wfp.Pscp.Sources.Add( "server001" );
        wfp.Pscp.Users = new List<UserType>();
        UserType user2 = new UserType();
        user2.Name = "user@server";
        user2.Password = "MyPassword";
        wfp.Pscp.Users.Add( user2 );

       String xml = wfp.Serialize( false );
       xml = xml.Substring( xml.IndexOf( "<" ) );
       return xml;

    }
}
