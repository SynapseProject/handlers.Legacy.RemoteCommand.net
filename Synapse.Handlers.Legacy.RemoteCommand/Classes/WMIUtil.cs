using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Management;
using System.IO;
using System.Threading;
using System.Diagnostics;
using System.Text.RegularExpressions;

//using Alphaleonis.Win32.Filesystem;

using config = Synapse.Handlers.Legacy.RemoteCommand.Properties.Settings;

namespace Synapse.Handlers.Legacy.RemoteCommand
{
    public static class WMIUtil
    {
        #region Public Methods - RunCommand

        public static Int32 RunCommand(String command, String server, String remoteWorkingDirectory, long timeoutMills, bool killProcessOnTimeout, TimeoutAction actionOnTimeout, bool isDryRun)
        {
            return RunCommand(command, server, remoteWorkingDirectory, null, null, null, timeoutMills, killProcessOnTimeout, actionOnTimeout, null, null, isDryRun);
        }

        public static Int32 RunCommand(String command, String server, String remoteWorkingDirectory, long timeoutMills, bool killProcessOnTimeout, TimeoutAction actionOnTimeout, Action<string, string> callback, string callbackLabel, bool isDryRun)
        {
            return RunCommand(command, server, remoteWorkingDirectory, null, null, null, timeoutMills, killProcessOnTimeout, actionOnTimeout, callback, callbackLabel, isDryRun);
        }

        public static Int32 RunCommand(String command, String server, String remoteWorkingDirectory, String runAsDomain, String runAsUser, String runAsPassword, long timeoutMills, bool killProcessOnTimeout, TimeoutAction actionOnTimeout, Action<string, string> callback, string callbackLabel, bool isDryRun)
        {
            if (config.Default.DebugMode)
                callback(callbackLabel, "DEBUG >> Inside RunCommand");

            Int32 exitStatus = 0;
            String rwd = config.Default.DefaultRemoteWorkingDirectory;
            
            if (string.IsNullOrWhiteSpace(rwd))
                rwd = @"C:\Temp";
            String rwdUnc = Utils.GetServerLongPath(server, rwd);
            String rwdWin = Utils.GetServerLongPathWindows(server, rwd);

            if (callback != null)
            {
                // Blank Out Any Plain Text Passwords
                string myCmd = command;
                if (command.ToLower().Contains("winrs "))
                    myCmd = Regex.Replace(myCmd, @"\s-p:[^\s\\]*", @" -p:********");
                
                if (command.ToLower().Contains("pscp ") || command.ToLower().Contains("plink "))
                    myCmd = Regex.Replace(myCmd, @"\s-pw [^\s\\]*", @" -pw ********");

                callback(callbackLabel, "Starting Command : " + myCmd);
            }

            if (!isDryRun)
            {
                try
                {
                    if (config.Default.DebugMode)
                        callback(callbackLabel, "DEBUG >> Setting / Creating Working Directory");

                    // Create the Remote Working Directory Using Defaults If None Is Passed In
                    if (remoteWorkingDirectory == null)
                        Utils.CreateDirectory(rwdUnc);
                    else
                    {
                        rwd = remoteWorkingDirectory;
                        rwdUnc = Utils.GetServerLongPath(server, remoteWorkingDirectory);
                        rwdWin = Utils.GetServerLongPathWindows(server, remoteWorkingDirectory);
                    }

                    // Create the process 
                    using (ManagementClass process = new ManagementClass("Win32_Process"))
                    {
                        if (config.Default.DebugMode)
                            callback(callbackLabel, "DEBUG >> Getting Management Scope");

                        ManagementScope scope = GetManagementScope(server, runAsDomain, runAsUser, runAsPassword);
                        if (config.Default.DebugMode)
                            callback(callbackLabel, "DEBUG >> Setting Process Management Scope");

                        process.Scope = scope;

                        if (config.Default.DebugMode)
                            callback(callbackLabel, "DEBUG >> Getting Process Method Parameters");

                        ManagementBaseObject inParams = process.GetMethodParameters("Create");

                        if (config.Default.DebugMode)
                            callback(callbackLabel, "DEBUG >> Creating Random Logfile Name");

                        String stdOutErrFile = System.IO.Path.GetRandomFileName();
                        stdOutErrFile = stdOutErrFile.Replace(".", "") + ".log";

                        if (config.Default.DebugMode)
                            callback(callbackLabel, "DEBUG >> Setting Process Method Parameters");

                        inParams["CurrentDirectory"] = rwd;
                        inParams["CommandLine"] = @"cmd.exe /c " + command + @" 1> " + stdOutErrFile + @" 2>&1";

                        if (config.Default.DebugMode)
                            callback(callbackLabel, "DEBUG >> Calling InvokeMethod");

                        ManagementBaseObject mbo = process.InvokeMethod("Create", inParams, null);

                        if (config.Default.DebugMode)
                            callback(callbackLabel, "DEBUG >> Called InvokeMethod, Checking ReturnValue");

                        UInt32 exitCode = (uint)mbo["ReturnValue"];
                        UInt32 processId = 0;

                        if (config.Default.DebugMode)
                            callback(callbackLabel, "DEBUG >> Return Value = [" + exitCode + "]");


                        if (exitCode == 0)
                        {
                            processId = (uint)mbo["ProcessId"];

                            if (config.Default.DebugMode)
                                callback(callbackLabel, "DEBUG >> Got ProcessId [" + processId + "]");

                            String uncOutFile = rwdUnc + @"\" + stdOutErrFile;
                            String winUncOutFile = rwdWin + @"\" + stdOutErrFile;
                            if (server == null || "localhost".Equals(server.ToLower()) || "127.0.0.1".Equals(server.ToLower()))
                            {
                                uncOutFile = rwd + @"\" + stdOutErrFile;
                                winUncOutFile = rwd + @"\" + stdOutErrFile;
                            }

                            // Start Tailing Output Log
                            if (config.Default.DebugMode)
                                callback(callbackLabel, "DEBUG >> Starting LogTrailer On File [" + winUncOutFile + "]");
                            LogTailer tailer = new LogTailer(winUncOutFile, callback, callbackLabel);
                            tailer.Start();

                            // Wait For Process To Finish or Timeout To Be Reached
                            ManagementEventWatcher w = new ManagementEventWatcher(scope, new WqlEventQuery("select * from Win32_ProcessStopTrace where ProcessId=" + processId));
                            if (timeoutMills > 0)
                                w.Options.Timeout = new TimeSpan(0, 0, 0, 0, (int)timeoutMills);
                            try
                            {
                                ManagementBaseObject mboEvent = w.WaitForNextEvent();
                                UInt32 uExitStatus = (UInt32)mboEvent.Properties["ExitStatus"].Value;
                                exitStatus = unchecked((int)uExitStatus);
                            }
                            catch (ManagementException ex)
                            {
                                if (ex.Message.Contains("Timed out"))
                                {
                                    StringBuilder rc = new StringBuilder();
                                    String processName = @"cmd.exe";
                                    String timeoutMessage = "TIMEOUT : Process [" + processName + "] With Id [" + processId + "] Failed To Stop In [" + timeoutMills + "] Milliseconds.";
                                    if (killProcessOnTimeout)
                                    {
                                        String queryStr = String.Format("SELECT * FROM Win32_Process Where Name = '{0}' AND ProcessId = '{1}'", processName, processId);
                                        ObjectQuery Query = new ObjectQuery(queryStr);
                                        ManagementObjectSearcher Searcher = new ManagementObjectSearcher(scope, Query);

                                        foreach (ManagementObject thisProcess in Searcher.Get())
                                            rc.Append(KillProcess(scope, thisProcess));

                                        using (StringReader procStr = new StringReader(rc.ToString()))
                                        {
                                            String procLine;
                                            while ((procLine = procStr.ReadLine()) != null)
                                            {
                                                if (callback != null)
                                                    callback(callbackLabel, procLine);
                                                else
                                                    Console.WriteLine(procLine);
                                            }
                                        }

                                        timeoutMessage = "TIMEOUT : Process [" + processName + "] With Id [" + processId + "] Failed To Stop In [" + timeoutMills + "] Milliseconds And Was Remotely Termintated.";
                                    }
                                    tailer.Stop(60, true);
                                    throw new Exception(timeoutMessage);
                                }
                                else
                                {
                                    tailer.Stop(60, true);
                                    throw ex;
                                }
                            }

                            // Process Completed.  Give up to 10 minutes for remote execution logs to be processed.
                            tailer.Stop(600, true);
                        }
                        else
                        {
                            if (callback != null)
                            {
                                callback(callbackLabel, "Return Value : " + exitCode);
                                callback(callbackLabel, mbo.GetText(TextFormat.Mof));
                            }
                        }
                    }
                }
                catch (Exception e)
                {
                    if (callback != null)
                    {
                        String errorMsg = e.Message;

                        if (errorMsg.StartsWith("TIMEOUT"))
                        {
                            callback(callbackLabel, e.Message);
                        }
                        else
                        {
                            callback(callbackLabel, "Error Occured In WMIUtils.RunCommand : ");
                            callback(callbackLabel, e.Message);
                            callback(callbackLabel, e.StackTrace);
                            throw e;
                        }
                    }

                    if (actionOnTimeout == TimeoutAction.Error)
                        throw e;
                }
            }
            else
            {
                callback?.Invoke(callbackLabel, "Dry Run Flag Set.  Execution Skipped");
            }

            if (callback != null)
                callback(callbackLabel, "Command Completed.  Exit Code = " + exitStatus);

            return exitStatus;
        }

        #endregion


        #region Private Methods
        
        static ManagementScope GetManagementScope(String server)
        {
            return GetManagementScope(server, null, null, null);
        }
        
        static ManagementScope GetManagementScope(String server, String domain, String userName, String password)
        {
            ConnectionOptions options = new ConnectionOptions();

            if (server != null)
            {
                if (!("localhost".Equals(server.Trim().ToLower())) && !("127.0.0.1".Equals(server.Trim())))
                {
                    if (domain != null && userName != null && password != null)
                    {
                        options.Impersonation = ImpersonationLevel.Impersonate;
                        options.Username = domain + @"\" + userName;
                        options.Password = password;
                    }
                }
                else
                    server = null;
            }
            options.Authentication = AuthenticationLevel.Default;
            options.Authority = null;
            options.EnablePrivileges = true;

            // Note: The ConnectionOptions object is not necessary 
            // if we are connecting to local machine & the account has privileges 
            ManagementScope scope = null;
            if (server == null)
                scope = new ManagementScope(@"\ROOT\CIMV2", options);
            else
                scope = new ManagementScope(@"\\" + server + @"\ROOT\CIMV2", options);
            scope.Connect();

            return scope;
        }

/*
        static void WaitForProcessToComplete(string server, string processName, int pid, ManagementScope scope, long timeoutMills, bool killProcessOnTimeout, string logFileToTail, Action<string, string> callback)
        {
            WaitForProcessToComplete(server, processName, pid, scope, timeoutMills, killProcessOnTimeout, logFileToTail, callback, server);
        }


        static void WaitForProcessToComplete(string server, string processName, int pid, ManagementScope scope, long timeoutMills, bool killProcessOnTimeout, string logFileToTail, Action<string, string> callback, string callbackLabel)
        {
            bool stillAlive = true;

            ObjectQuery query = new ObjectQuery(
                string.Format("SELECT ProcessId FROM Win32_Process WHERE Name = '{0}' AND ProcessId = '{1}'", processName, pid));

            EnumerationOptions eo = new EnumerationOptions();
            eo.Rewindable = false;
            eo.ReturnImmediately = true;
            eo.EnumerateDeep = false;
            eo.EnsureLocatable = false;
            eo.DirectRead = true;
            eo.EnsureLocatable = false;
            eo.UseAmendedQualifiers = false;
            eo.BlockSize = 10;
            eo.Timeout = new TimeSpan(0, 0, 0, 5);

            ManagementObjectSearcher mos = new ManagementObjectSearcher(scope, query, eo);

            Stopwatch clock = new Stopwatch();
            if (timeoutMills > 0)   
                clock.Start();

            StreamReader reader = null;
            if (logFileToTail != null)
            {
                try { reader = new StreamReader(new FileStream(logFileToTail, FileMode.Open, FileAccess.Read, FileShare.ReadWrite)); }
                catch { reader = null; }
            }
            long lastMaxOffset = 0;
            String line = String.Empty;

            while (stillAlive)
            {
                stillAlive = false;

                try
                {
                    ManagementObjectCollection processes = mos.Get();

                    foreach (ManagementObject process in processes)
                    {
                        object prId = process.GetPropertyValue("ProcessId");
                        stillAlive = true;
                    }
                }
                catch (Exception e)
                {
                    stillAlive = true;
                    callback(callbackLabel, "ERROR : Error occured polling for process status : " + e.Message);
                }


                if (reader == null && logFileToTail != null && stillAlive == false)
                {
                    // Process Is Dead, But Still Haven't Been Able To Get The Output File.  
                    // Pause A Bit And Give It One Last Try.
                    Thread.Sleep(5000);
                    try { reader = new StreamReader(new FileStream(logFileToTail, FileMode.Open, FileAccess.Read, FileShare.ReadWrite)); }
                    catch
                    {
                        reader = null;
                        if (callback != null)
                            callback(callbackLabel, "Process Completed But Unable To Find Output File [" + logFileToTail + "].");
                        else
                            Console.WriteLine(">> Process Completed But Unable To Find Output File [" + logFileToTail + "].");
                    }
                }


                if (reader != null)
                {
                    if (reader.BaseStream.Length != lastMaxOffset)
                    {
                        //seek to the last max offset
                        reader.BaseStream.Seek(lastMaxOffset, SeekOrigin.Begin);

                        int i = 0;
                        //read out of the file until the EOF
                        while ((i = reader.Read()) >= 0)
                        {
                            char ch = (char)i;
                            if (ch == '\r' || ch == '\n')
                            {
                                if (callback != null)
                                    callback(callbackLabel, line);
                                else
                                    Console.WriteLine(">>" + line);

                                line = String.Empty;

                                // Check To See If Next Character Is Also A LF or CR and Burn It If It Is.
                                char nextCh = (char)reader.Peek();
                                if (nextCh == '\r' || nextCh == '\n')
                                    reader.Read();
                            }
                            else
                                line += ch;
                        }

                        //update the last max offset
                        lastMaxOffset = reader.BaseStream.Position;

                    }
                }
                else
                {
                    if (logFileToTail != null)
                    {
                        try { reader = new StreamReader(new FileStream(logFileToTail, FileMode.Open, FileAccess.Read, FileShare.ReadWrite)); }
                        catch { reader = null; }
                    }
                }

                if (timeoutMills > 0 && clock.ElapsedMilliseconds >= timeoutMills)
                {
                    StringBuilder rc = new StringBuilder();
                    clock.Stop();
                    String timeoutMessage = "Process [" + processName + "] With Id [" + pid + "] Failed To Stop In [" + timeoutMills + "] Milliseconds.";
                    if (killProcessOnTimeout)
                    {
                        String queryStr = String.Format("SELECT * FROM Win32_Process Where Name = '{0}' AND ProcessId = '{1}'", processName, pid);
                        ObjectQuery Query = new ObjectQuery(queryStr);
                        ManagementObjectSearcher Searcher = new ManagementObjectSearcher(scope, Query);

                        foreach (ManagementObject process in Searcher.Get())
                            rc.Append(KillProcess(scope, process));

                        using (StringReader procStr = new StringReader(rc.ToString()))
                        {
                            String procLine;
                            while ((procLine = procStr.ReadLine()) != null)
                            {
                                if (callback != null)
                                    callback(callbackLabel, procLine);
                                else
                                    Console.WriteLine(procLine);
                            }
                        }

                        timeoutMessage = "Process [" + processName + "] With Id [" + pid + "] Failed To Stop In [" + timeoutMills + "] Milliseconds And Was Remotely Termintated.";
                    }

                    throw new Exception(timeoutMessage);
                }

                Thread.Sleep(1000);
            } // End While StillAlive

            // Close the Reader
            if (reader != null)
            {
                reader.Close();
                reader.Dispose();
            }


        }
 */
        static String KillProcess(ManagementScope scope, ManagementObject process)
        {
            StringBuilder rc = new StringBuilder();
            String queryStr = String.Format("SELECT * FROM Win32_Process Where ParentProcessId = '{0}'", process.GetPropertyValue("ProcessId"));
            ObjectQuery Query = new ObjectQuery(queryStr);
            ManagementObjectSearcher Searcher = new ManagementObjectSearcher(scope, Query);

            foreach (ManagementObject childProc in Searcher.Get())
                rc.Append(KillProcess(scope, childProc));

            rc.AppendLine("Terminated Process : [" + process.GetPropertyValue("Name") + "] With PID [" + process.GetPropertyValue("ProcessId") + "]");
            process.InvokeMethod("Terminate", null);

            return rc.ToString();
        }

        #endregion
    }
}
