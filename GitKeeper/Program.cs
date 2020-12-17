using ConsoleAppFramework;
using Kurukuru;
using LibGit2Sharp;
using Microsoft.Extensions.Hosting;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using SystemEx;

namespace GitKeeper
{
#pragma warning disable CS1998 // Async method lacks 'await' operators and will run synchronously
	class Program : ConsoleAppBase
	{
		protected static CancellationTokenSource cancel = new CancellationTokenSource();

		protected Dictionary<GitRepositoryGroup, List<GitRepository>> repositoryByGroup = new Dictionary<GitRepositoryGroup, List<GitRepository>>();
		protected List<GitRepository> repositoryWithoutGroup = new List<GitRepository>();

		static async Task Main(string[] args)
		{
			Console.CancelKeyPress += Cancel;
			await Host.CreateDefaultBuilder().RunConsoleAppFrameworkAsync<Program>(args)
				.ConfigureAwait(false);
		}

		[Command("status")]
		public async Task Status()
		{
			Console.WriteLine($"Configuration path: {Configuration.ConfigurationFolderPath}");
		}

		[Command("scan")]
		public async Task Scan([Option(0)] string path, [Option(1)] string name = "active")
		{
			await ProgressSpinner<int>.StartAsync("Searching repositories...", p => { }, async spinner => {
				await SearchRepositories(path, progress: spinner, cancellationToken: cancel.Token).ConfigureAwait(false);
				await SaveConfiguration(name).ConfigureAwait(false);
			}).ConfigureAwait(false);
		}

		[Command("sync")]
		public async Task Sync([Option(0)] string name = "active")
		{
			var active = Configuration.EnumConfigurations()
				.Where(c => c.name == name)
				.FirstOrDefault();

			if (active == null)
				return;

			await LoadConfiguration(active).ConfigureAwait(false);

			//var signature = new Signature(
			//	new Identity("git-keeper", "no-email@google.com")
			//	, DateTimeOffset.Now);

			ForeachRepositoryBranch(branch => {
				var sorted = branch.Select(r => new { repository = r, date = r.Head.Tip.Author.When })
					.ToList();
				if (sorted.Count < 2)
					return;

				sorted.Sort((a, b) => a.date.CompareTo(b.date));

				for (int i = 0; i < sorted.Count; i++)
				{
					var lastRepository = sorted.Last();
					var currentRepository = sorted[i];

					if (currentRepository.date.Equals(lastRepository.date))
						break;

					Console.WriteLine($"{currentRepository.repository.Path} >> {lastRepository.repository.Path}");
					using (Utilities.SetCurrentDirectory(currentRepository.repository.Path))
					{
						Utilities.ExecuteCommandLine(
							Utilities.EscapeCommandLineArgs("git", "pull", lastRepository.repository.Path));
					}
					//Commands.Pull(currentRepository.repository.Repository
					//	, lastRepository.repository.Path
					//	, signature);
					//sorted[i].repository
				}
			});
		}

		[Command("check")]
		public async Task Check([Option(0)] string name = "active")
		{
			var active = Configuration.EnumConfigurations()
				.Where(c => c.name == name)
				.FirstOrDefault();

			if (active == null)
				return;

			await LoadConfiguration(active).ConfigureAwait(false);

			ForeachRepositoryBranch(branch => {
				foreach (var repository in branch)
				{
					var isConflict = repository.Repository.Index.Conflicts.Any();
					var isDirty = repository.Repository.RetrieveStatus().IsDirty;

					if (isConflict || isDirty)
					{
						Console.WriteLine($"{repository.Path} {string.Join(" ", isConflict ? "have conflicts" : "", isDirty ? "have changes" : "")}");
					}
				}
			});
		}

		protected static void Cancel(object sender, ConsoleCancelEventArgs args)
		{
			args.Cancel = true;
			cancel.Cancel();
		}

		protected async Task SearchRepositories(string path
			, IProgress<int> progress = default
			, CancellationToken cancellationToken = default)
		{
			try
			{
				foreach (var rep in GitRepository.EnumGitRepositories(path, progress, cancellationToken))
				{
					if (rep == null)
						continue;

					var group = new GitRepositoryGroup(rep);
					Console.WriteLine("{1}:{2} {0}", rep.Path, group.id, rep.Head?.FriendlyName);

					if (group.IsValid)
					{
						if (!repositoryByGroup.TryGetValue(group, out var list))
						{
							list = new List<GitRepository>();
							repositoryByGroup.Add(group, list);
						}

						list.Add(rep);
					}
					else
					{
						repositoryWithoutGroup.Add(rep);
					}
				}
			}
			catch (AggregateException e)
			{
				Console.WriteLine(e);
			}
		}

		void ForeachRepositoryBranch(Action<IGrouping<string, GitRepository>> fn)
		{
			foreach (var (_, repository) in repositoryByGroup)
			{
				foreach (var branch in repository.GroupBy(r => r.Head?.FriendlyName).Where(g => !g.Key.null_ws_()))
				{
					fn(branch);
				}
			}
		}

		class KeyValuePairComparer<TKey, TValue> : IEqualityComparer<KeyValuePair<TKey, TValue>>
		{
			public bool Equals(KeyValuePair<TKey, TValue> x, KeyValuePair<TKey, TValue> y)
			{
				return x.Key.Equals(y.Key);
			}
			public int GetHashCode(KeyValuePair<TKey, TValue> x)
			{
				return x.GetHashCode();
			}
		}

		protected async Task LoadConfiguration(Configuration c)
		{
			//repositoryByGroup.Union(c.repositories, new KeyValuePairComparer<GitRepositoryGroup, List<GitRepository>>());
			c = c.Read();
			repositoryByGroup = c.repositories
				.ToDictionary(k => new GitRepositoryGroup { id = k.id }
					, v => v.instances.Select(ri => new GitRepository(ri.path))
					.ToList());
			repositoryWithoutGroup = c.faulty
				.Select(ri => new GitRepository(ri.path))
				.ToList();
			/*
			foreach (var rc in c.repositories)
			{
				var group = new GitRepositoryGroup { id = rc.id };
				if (!repositoryByGroup.TryGetValue(group, out var list))
				{
					list = new List<GitRepository>();
					repositoryByGroup.Add(group, list);
				}

				list.AddRange(rc.instances.Select(rci => new GitRepository(rci.path))));
			}
			*/
		}

		protected async Task SaveConfiguration(string name)
		{
			var c = new Configuration {
				repositories = repositoryByGroup
					.Select(r => new Configuration.Repository {
						id = r.Key.id,
						instances = r.Value
									.Select(ri => new Configuration.RepositoryInstance { path = ri.Path, branch = ri.Head?.FriendlyName })
									.ToList()
					})
					.ToList(),
				faulty = repositoryWithoutGroup
					.Select(ri => new Configuration.RepositoryInstance { path = ri.Path, branch = ri.Head?.FriendlyName })
					.ToList()
			};
			Configuration.SaveConfiguration(name, c);
		}
	}
#pragma warning restore CS1998 // Async method lacks 'await' operators and will run synchronously
}
