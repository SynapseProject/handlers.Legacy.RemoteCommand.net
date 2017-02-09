using System;
using System.Threading;
using System.Diagnostics;

namespace Synapse.Handlers.Legacy.RemoteCommand
{
    class LocalProcessUtil
    {
        public static Int32 RunCommand(String command, String args, String remoteWorkingDirectory, long timeoutMills, TimeoutAction actionOnTimeout, Action<string, string> callback, String callbackLabel, bool isDryRun)
        {
            Int32 exitCode = 0;

            Process process = new Process();
            process.StartInfo.FileName = command;
            process.StartInfo.Arguments = args;
            process.StartInfo.WorkingDirectory = remoteWorkingDirectory;
            process.StartInfo.WindowStyle = ProcessWindowStyle.Hidden;
            process.StartInfo.CreateNoWindow = true;
            process.StartInfo.RedirectStandardOutput = true;
            process.StartInfo.RedirectStandardError = true;
            process.StartInfo.UseShellExecute = false;

            if (callback != null)
                callback(callbackLabel, "Starting Command : " + command + " " + args);

            if (!isDryRun)
            {
                process.Start();


                Thread stdOutReader = new Thread(delegate ()
                {
                    while (!process.StandardOutput.EndOfStream)
                    {
                        String line = process.StandardOutput.ReadLine();
                        callback(callbackLabel, line);
                    }
                });
                stdOutReader.Start();

                Thread stdErrReader = new Thread(delegate ()
                {
                    while (!process.StandardError.EndOfStream)
                    {
                        String line = process.StandardError.ReadLine();
                        callback(callbackLabel, line);
                    }
                });
                stdErrReader.Start();


                bool timeoutReached = false;
                Stopwatch timer = new Stopwatch();
                timer.Start();
                while (stdOutReader.IsAlive && stdErrReader.IsAlive && !(timeoutReached))
                {
                    if (timeoutMills > 0)
                    {
                        if (timer.ElapsedMilliseconds > timeoutMills)
                            timeoutReached = true;
                    }
                    Thread.Sleep(500);
                }
                timer.Stop();

                if (timeoutReached)
                {
                    String timeoutMessage = "TIMEOUT : Process [" + process.ProcessName + "] With Id [" + process.Id + "] Failed To Stop In [" + timeoutMills + "] Milliseconds And Was Remotely Termintated.";

                    if (!process.HasExited)
                    {
                        process.Kill();
                        callback(callbackLabel, timeoutMessage);
                    }
                    else
                    {
                        timeoutMessage = "TIMEOUT : Process [" + process.ProcessName + "] With Id [" + process.Id + "] Failed To Stop In [" + timeoutMills + "] Milliseconds But May Have Completed.";
                        callback(callbackLabel, timeoutMessage);
                    }
                    if (actionOnTimeout == TimeoutAction.Error)
                        throw new Exception(timeoutMessage);
                }

                exitCode = process.ExitCode;
            }
            else
            {
                callback?.Invoke(callbackLabel, "Dry Run Flag Set.  Execution Skipped");
            }

            if (callback != null)
                callback(callbackLabel, "Command Completed.  Exit Code = " + exitCode);

            return exitCode;
        }

    }
}
