using System;
using System.Diagnostics;
using System.IO;
using System.Collections.Generic;

namespace ForkStalker
{
	public enum RegressType { play, mono };

	public class Regression
	{
		const String scriptName = "CI/{0}.regression.sh";
		readonly ProcessStartInfo regressStartInfo;
		readonly RegressType rType;
		readonly public List<string> outputStdOut;
		readonly public List<string> outputStdErr;
		int lineCount = 0; 
		Boolean Hide;

		public Regression (RegressType regType, String repoDir)
		{
			rType = regType;
			regressStartInfo = new ProcessStartInfo ();
			regressStartInfo.FileName = Path.Combine (repoDir, String.Format (scriptName, regType));
			regressStartInfo.WorkingDirectory = repoDir;
			regressStartInfo.RedirectStandardError = true;
			regressStartInfo.RedirectStandardOutput = true;
			regressStartInfo.RedirectStandardInput = true;
			regressStartInfo.UseShellExecute = false;
			regressStartInfo.CreateNoWindow = true;
			outputStdOut = new List<string>();
			outputStdErr = new List<string>();
		}

		public int RunRegression(Boolean Hide = false) {
			this.Hide = Hide;
			outputStdOut.Clear ();
			outputStdErr.Clear ();
			var regressProcess = new Process ();
			regressProcess.OutputDataReceived += StdOutDataReceivedEventHandler;
			regressProcess.ErrorDataReceived += StdErrorDataReceivedEventHandler;
			regressProcess.StartInfo = regressStartInfo;
			regressProcess.Start ();
			regressProcess.BeginErrorReadLine ();
			regressProcess.BeginOutputReadLine ();
			regressProcess.WaitForExit();
			var res = regressProcess.ExitCode;
			regressProcess.Close ();
			return res;
		}

	 	void StdErrorDataReceivedEventHandler(object sender, DataReceivedEventArgs e) {
			if (!String.IsNullOrEmpty(e.Data))
			{
				outputStdErr.Add (e.Data);
				Trace(e.Data);
			}
		}

		void StdOutDataReceivedEventHandler(object sender, DataReceivedEventArgs e) {
			if (!String.IsNullOrEmpty(e.Data))
			{
				outputStdOut.Add(e.Data);
				Trace (e.Data);
			}
		}

		void Trace(string output) {
			if (Hide)
				return;
			lineCount++;
			Console.WriteLine ("[{0}]: {1}", lineCount.ToString().PadLeft(4, '0'), output);
		}
	}
}

