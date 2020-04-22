using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace ConsoleApp3
{
	public static class FileDirectorySearcher
	{
		public static Regex WildcardToRegex(string pattern)
		{
			return new Regex("^" + Regex.Escape(pattern)
				.Replace("\\*", ".*", StringComparison.InvariantCulture)
				.Replace("\\?", ".", StringComparison.InvariantCulture) + "$");
		}

		private static IEnumerable<string> EnumerateFileSystemEntries(string root)
		{
			Stack<string> roots = new Stack<string>();
			roots.Push(root);

			while (roots.Count > 0)
			{
				string d = roots.Pop();

				IEnumerable<string> entries = null;
				try
				{
					entries = Directory.EnumerateFileSystemEntries(d, "*", SearchOption.TopDirectoryOnly);
				}
				catch (UnauthorizedAccessException) { continue; }
				catch (DirectoryNotFoundException) { continue; }

				foreach (string path in entries)
				{
					yield return path;

					if (Directory.Exists(path))
					{
						roots.Push(path);
					}
				}
			}
		}

		public static IEnumerable<string> Search(string searchPath, string searchPattern, IProgress<int> progress = null)
		{
			return Search(searchPath, WildcardToRegex(searchPattern), progress);
		}

		public static IEnumerable<string> Search(string searchPath, Regex match, IProgress<int> progress = null)
		{
			Contract.Requires(match != null);

			foreach (string path in EnumerateFileSystemEntries(searchPath))
			{
				progress?.Report(1);

				if (match.IsMatch(Path.GetFileName(path)))
					yield return path;
			}
		}

		public static async IAsyncEnumerable<string> SearchAsync(string searchPath, string searchPattern
			, IProgress<int> progress = default
			, [EnumeratorCancellation] CancellationToken cancellationToken = default)
		{
			await foreach (var path in SearchAsync(searchPath, WildcardToRegex(searchPattern), progress).WithCancellation(cancellationToken))
				yield return path;
		}

		public static async IAsyncEnumerable<string> SearchAsync(string searchPath, Regex match
			, IProgress<int> progress = default
			, [EnumeratorCancellation] CancellationToken cancellationToken = default)
		{
			Contract.Requires(match != null);

			foreach (var path in EnumerateFileSystemEntries(searchPath)
				.AsParallel().WithCancellation(cancellationToken)
				.Select((path) =>
				{
					progress?.Report(1);

					if (match.IsMatch(Path.GetFileName(path)))
						return path;

					return string.Empty;
				})
				.Where((path) => !string.IsNullOrWhiteSpace(path)))
			{
				yield return path;
			}
		}
	}
}
