using System;
using System.ComponentModel;

namespace Synapse.Handlers.Legacy.RemoteCommand
{
	public enum ScriptType
	{
        None,
        Ant,
        Powershell,
        Plink,
        Pscp,
	}

    public enum ArgumentType
    {
        None, 
        Named,
        Ordered,
        Xml
    }

    public enum RunAsProtocolType
    {
        None,
        WinRm
    }

    public enum RunUsingProtocolType
    {
        WMI,
        WinRm,
        ProcessStart
    }

    public enum Operators
    {
        EqualTo,
        NotEqualTo,
        LessThan,
        LessThanOrEqualTo,
        GreaterThan,
        GreaterThanOrEqualTo,
        Between,
        NotBetween
    }

    public enum TimeoutAction
    {
        Continue,
        Error
    }

    public enum ExecutionPolicyType
    {
        None,
        Bypass,
        Restricted,
        AllSigned,
        RemoteSigned,
        Unrestricted,
        Undefined
    }
}