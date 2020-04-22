using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace ConsoleApp3
{
	class ConsoleProgress : IProgress<int>, IDisposable
	{
		static readonly char[] p = new char[] { '\\', '|', '/', '-' };

		int i = -1;
		int cx, cy;

		public ConsoleProgress()
		{
			Reset();
		}

		public void Report(int value)
		{
			i += value;

			Console.SetCursorPosition(cx, cy);
			Console.Write(p[i % p.Length]);
		}

		public ConsoleProgress Clear()
		{
			Console.SetCursorPosition(cx, cy);
			Console.Write(' ');

			return this;
		}

		public ConsoleProgress Reset()
		{
			i = -1;

			cx = Console.CursorLeft;
			cy = Console.CursorTop;

			return this;
		}

		public void Dispose()
		{
			Clear();
		}
	}

	class Program : IDisposable
	{
		protected static CancellationTokenSource cancel = new CancellationTokenSource();

		protected Dictionary<GitRepositoryGroup, List<GitRepository>> repositoryByGroup = new Dictionary<GitRepositoryGroup, List<GitRepository>>();
		protected List<GitRepository> repositoryWithoutGroup = new List<GitRepository>();

		static async Task Main(string[] args)
		{
			Console.CancelKeyPress += Cancel;

			Console.WriteLine("Hello World!");
			await RunProgram(args).ConfigureAwait(false);
		}

		public static async Task RunProgram(string[] args)
		{
			using (Program p = new Program(args))
				await p.Run(args);
		}

		protected Program(string[] args)
		{

		}

		protected async Task Run(string[] args)
		{
			Stopwatch sw = Stopwatch.StartNew();

#if false
			//			using (var progress = new ConsoleProgress())
			{
				foreach (var rep in GitRepository.EnumGitRepositories(args[0]/*, progress*/))
				{
//					progress.Clear();
					Console.WriteLine("{1}:{2} {0}", rep.Path, rep.InitialCommit?.Sha, rep.Head?.FriendlyName);
//					progress.Reset();
				}
			}

			Console.WriteLine("{0} ms sync version.", sw.ElapsedMilliseconds);
			sw = Stopwatch.StartNew();
#endif
			var active = Configuration.EnumConfigurations()
				.Where(c => c.name == $"active")
				.FirstOrDefault();

			if (active != null)
			{
				await LoadConfiguration(active);

				int i = 0;
			}
			else
			{
				await SearchRepositories(args[0], cancellationToken: cancel.Token);

				await SaveConfiguration("active");
			}
		}

		public void Dispose()
		{
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
				await foreach (var rep in GitRepository.EnumGitRepositoriesAsync(path, progress, cancellationToken))
				{
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
			catch (AggregateException)
			{
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
			var c = new Configuration
			{
				repositories = repositoryByGroup.Select(r => new Configuration.Repository
				{
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
}
