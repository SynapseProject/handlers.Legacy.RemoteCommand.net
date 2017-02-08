using System;
using System.Diagnostics;
using System.Text.RegularExpressions;
using Sentosa.CommandCenter.Adapters.Core;
using Sentosa.CommandCenter.Api.Client;

namespace Sentosa.CommandCenter.Adapters.Database.Core
{
	public class ProcessUtil
	{
		public event EventHandler<AdapterProgressEventArgs> StepProgress;

		Script _script = null;

		public ProcessUtil(Script script)
		{
			_script = script;
		}

		public void Start()
		{
			Process p = new Process();
			p.StartInfo.FileName = "Database.TestClient.exe";
			p.StartInfo.Arguments = _script.Path;
			p.StartInfo.UseShellExecute = false;
			p.StartInfo.CreateNoWindow = true;
			p.StartInfo.WindowStyle = ProcessWindowStyle.Hidden;
			p.StartInfo.RedirectStandardOutput = true;
			p.StartInfo.RedirectStandardError = true;

			p.EnableRaisingEvents = true;
			p.OutputDataReceived += p_OutputDataReceived;
			p.ErrorDataReceived += p_ErrorDataReceived;

			p.Start();

			// best practice information on accessing stdout/stderr from mdsn article:
			//  https://msdn.microsoft.com/en-us/library/system.diagnostics.processstartinfo.redirectstandardoutput%28v=vs.110%29.aspx
			// Do not wait for the child process to exit before reading to the end of its redirected stream.
			// Do not perform a synchronous read to the end of both redirected streams.
			// string output = p.StandardOutput.ReadToEnd();
			// string error = p.StandardError.ReadToEnd();
			// p.WaitForExit();
			// Use asynchronous read operations on at least one of the streams.
			p.BeginOutputReadLine();
			p.BeginErrorReadLine();


			p.WaitForExit();
		}

		void p_OutputDataReceived(object sender, DataReceivedEventArgs e)
		{
			OnStepProgress( string.Format( "OutputData:{0}", _script.Path ), e.Data );
		}

		void p_ErrorDataReceived(object sender, DataReceivedEventArgs e)
		{
			_script.Success = false;

			bool is500 = false;
			if( !string.IsNullOrWhiteSpace( e.Data ) )
			{
				Match match = Regex.Match( e.Data, "500" );
				is500 = match.Success;
			}

			if( is500 )
			{
				OnStepProgress( string.Format( "ErrorData: ------------>{0}", _script.Path ), e.Data );
			}
			else
			{
				OnStepProgress( string.Format( "------------> ErrorData:{0}", _script.Path ), e.Data );
			}
		}

		/// <summary>
		/// Notify of step progress.
		/// </summary>
		/// <param name="context">The method name or workflow activty.</param>
		/// <param name="message">Descriptive message.</param>
		/// <param name="status">Overall Package status indicator.</param>
		/// <param name="id">Message Id.</param>
		/// <param name="severity">Message/error severity.</param>
		/// <param name="ex">Current exception (optional).</param>
		protected virtual void OnStepProgress(string context, string message, PackageStatus status = PackageStatus.Running, int id = 0, int severity = 0, Exception ex = null)
		{
			if( StepProgress != null )
			{
				StepProgress( this, new AdapterProgressEventArgs( context, message, status, id, severity, ex ) );
			}
		}
	}
}