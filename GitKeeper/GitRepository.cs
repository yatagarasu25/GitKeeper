using LibGit2Sharp;
using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using SystemEx;

namespace GitKeeper
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

		public Repository Repository => repository;
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


		protected static GitRepository OpenGitRepository(string path, AggregateExceptionScope e_)
		{
			try
			{
				return new GitRepository(path);
			}
			catch (Exception e)
			{
				e_.Aggregate(e);
			}

			return null;
		}

		public static IEnumerable<GitRepository> EnumGitRepositories(string rootPath
			, IProgress<int> progress = default
			, CancellationToken cancellationToken = default)
		{
			return FileDirectorySearcher.Search(rootPath, ".git", progress, cancellationToken)
				.SelectValid(gitMarker => {
					using var e_ = new AggregateExceptionScope();

					var path = System.IO.Path.GetDirectoryName(gitMarker);
					return OpenGitRepository(path, e_);
				}).ToArray();
		}
	}
}
