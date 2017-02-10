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
        Console.WriteLine(wfp.ToString());

        Workflow wf = Workflow.GetInstance(wfp);

        wf.OnLogMessage = this.OnLogMessage;
        wf.OnProgress = this.OnProgress;

//        wf.StepStarting += wf_StepStarting;
//        wf.StepProgress += wf_StepProgress;
//        wf.StepFinished += wf_StepFinished;

        seqNo = 0;
        OnProgress("Execute", "Starting", StatusType.Running, startInfo.InstanceId, seqNo++);
        wf.ExecuteAction(startInfo.IsDryRun);
        OnProgress("Execute", "Completed", StatusType.Complete, startInfo.InstanceId, seqNo++);

        return new ExecuteResult() { Status = StatusType.Complete };
    }

    void wf_StepStarting(object sender, AdapterProgressEventArgs e)
    {
        OnLogMessage(e.Context, e.Message);
    }

    void wf_StepProgress(object sender, AdapterProgressEventArgs e)
    {
        OnLogMessage(e.Context, e.Message);
    }

    void wf_StepFinished(object sender, AdapterProgressEventArgs e)
    {
        OnLogMessage(e.Context, e.Message);
    }

}
