using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;

namespace GitKeeper
{
	public static class Utilities
	{
		public static string EscapeCommandLineArgs(params string[] args)
		{
			return string.Join(" ", args.Select((a) => {
				var s = Regex.Replace(a, @"(\\*)" + "\"", @"$1$1\" + "\"");
				if (s.Contains(" "))
				{
					s = "\"" + Regex.Replace(s, @"(\\+)$", @"$1$1") + "\"";
				}
				return s;
			}).ToArray());
		}

		public static void ExecuteCommandLine(string command)
		{
			Console.WriteLine("> " + command);

			ProcessStartInfo processStartInfo = new ProcessStartInfo();
			processStartInfo.UseShellExecute = false;
			if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
			{
				processStartInfo.FileName = "cmd.exe";
				processStartInfo.Arguments = "/C \"" + command + "\"";
			}
			else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
			{
				processStartInfo.FileName = "sh";
				processStartInfo.Arguments = command;
			}

			using (Process process = Process.Start(processStartInfo))
			{
				//process.StartInfo.RedirectStandardOutput = true;
				//process.StartInfo.RedirectStandardError = true;
				//process.StartInfo.RedirectStandardInput = true;
				//process.BeginOutputReadLine();
				//process.BeginErrorReadLine();

				//Console.Write(process.StandardOutput.ReadToEnd());
				//Console.Write(process.StandardError.ReadToEnd());

				process.WaitForExit();
			}
		}

		public static void ExecuteOpenFile(string filename)
		{
			Console.WriteLine("> " + filename);

			ProcessStartInfo processStartInfo = new ProcessStartInfo();
			processStartInfo.UseShellExecute = true;
			processStartInfo.WorkingDirectory = Path.GetDirectoryName(filename);
			processStartInfo.FileName = filename;
			processStartInfo.Verb = "OPEN";
			using (Process process = Process.Start(processStartInfo))
			{
				//process.StartInfo.RedirectStandardOutput = true;
				//process.StartInfo.RedirectStandardError = true;
				//process.StartInfo.RedirectStandardInput = true;
				//process.BeginOutputReadLine();
				//process.BeginErrorReadLine();

				//Console.Write(process.StandardOutput.ReadToEnd());
				//Console.Write(process.StandardError.ReadToEnd());
			}
		}

		public static string ClampPath(string fullpath, string prefixpath)
		{
			int prefixLength = prefixpath.Length + 1;

			if (fullpath.Length > prefixLength)
				return fullpath.Substring(prefixLength) + "/";

			return string.Empty;
		}

		class PreventSleepGuard : IDisposable
		{
			public PreventSleepGuard()
			{
				SetThreadExecutionState(ExecutionState.EsContinuous | ExecutionState.EsSystemRequired);
			}

			public void Dispose()
			{
				SetThreadExecutionState(ExecutionState.EsContinuous);
			}
		}

		public static IDisposable PreventSleep()
		{
			return new PreventSleepGuard();
		}

		[DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
		private static extern ExecutionState SetThreadExecutionState(ExecutionState esFlags);

		[Flags]
		private enum ExecutionState : uint
		{
			EsAwaymodeRequired = 0x00000040,
			EsContinuous = 0x80000000,
			EsDisplayRequired = 0x00000002,
			EsSystemRequired = 0x00000001
		}

		class SetCurrentDirectoryGuard : IDisposable
		{
			string olddir;

			public SetCurrentDirectoryGuard(string newdir)
			{
				olddir = Directory.GetCurrentDirectory();
				Directory.SetCurrentDirectory(newdir);
			}

			public void Dispose()
			{
				Directory.SetCurrentDirectory(olddir);
			}
		}

		public static IDisposable SetCurrentDirectory(string newdir)
		{
			return new SetCurrentDirectoryGuard(newdir);
		}
	}
}
