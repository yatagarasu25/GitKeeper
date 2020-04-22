using LibGit2Sharp;
using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;

namespace ConsoleApp3
{
	public struct GitRepositoryGroup
	{
		public string id;

		public bool IsValid => id != null;

		public GitRepositoryGroup(GitRepository repository)
		{
			Contract.Requires(repository != null);

			id = repository.InitialCommit?.Sha;
		}

		public override bool Equals(object obj)
		{
			if (obj is GitRepositoryGroup g)
			{
				return id == g.id;
			}

			return false;
		}

		public override int GetHashCode()
		{
			return id != null ? id.GetHashCode() : 0;
		}

		public static bool operator ==(GitRepositoryGroup left, GitRepositoryGroup right)
		{
			return left.Equals(right);
		}

		public static bool operator !=(GitRepositoryGroup left, GitRepositoryGroup right)
		{
			return !(left == right);
		}
	}

	public class GitRepository : IDisposable
	{
		readonly string path;
		readonly Repository repository;
		readonly Commit initialCommit;

		public string Path => path;
		public Commit InitialCommit => initialCommit;
		public Branch Head => repository.Head;

		public GitRepository(string path)
		{
			this.path = path;
			repository = new Repository(path);
			initialCommit = repository.Commits.LastOrDefault();
		}

		public void Dispose()
		{
			repository.Dispose();
		}


		public class AggregateExceptionSource : IDisposable
		{
			public List<Exception> exs = new List<Exception>();

			public void Dispose()
			{
				if (exs.Count > 0)
					throw new AggregateException(exs);
			}
		}

		protected static GitRepository OpenGitRepository(string path, AggregateExceptionSource e_)
		{
			try
			{
				return new GitRepository(path);
			}
			catch (Exception e)
			{
				e_.exs.Add(e);
			}

			return null;
		}

		public static IEnumerable<GitRepository> EnumGitRepositories(string rootPath, IProgress<int> progress = default)
		{
			using (var e_ = new AggregateExceptionSource())
			{
				foreach (var gitMarker in FileDirectorySearcher.Search(rootPath, ".git", progress))
				{
					var path = System.IO.Path.GetDirectoryName(gitMarker);
					yield return OpenGitRepository(path, e_);
				}
			}
		}

		public static async IAsyncEnumerable<GitRepository> EnumGitRepositoriesAsync(string rootPath
			, IProgress<int> progress = default
			, [EnumeratorCancellation] CancellationToken cancellationToken = default)
		{
			using (var e_ = new AggregateExceptionSource())
			{
				await foreach (var gitMarker in FileDirectorySearcher.SearchAsync(rootPath, ".git", progress, cancellationToken))
				{
					var path = System.IO.Path.GetDirectoryName(gitMarker);
					yield return OpenGitRepository(path, e_);
				}
			}
		}
	}
}
