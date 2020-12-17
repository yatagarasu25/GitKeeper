using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using SystemEx;

namespace GitKeeper
{
	public class Configuration
	{
		public struct Repository
		{
			public string id;

			public List<RepositoryInstance> instances;
		}

		public struct RepositoryInstance
		{
			public string path;
			public string branch;
		}

		[NonSerialized] public string path_;
		[NonSerialized] public string name;
		public string path {
			get => path_;
			set {
				path_ = value;
				name = Path.GetFileName(path_).CutEnd(_ext_.Length);
			}
		}
		public List<Repository> repositories;
		public List<RepositoryInstance> faulty;



		static Configuration()
		{
			if (!Directory.Exists(ConfigurationFolderPath))
				Directory.CreateDirectory(ConfigurationFolderPath);
		}

		public Configuration Read()
		{
			var c = ReadConfiguration(File.ReadAllText(path));
			c.path = path;
			return c;
		}

		public static Configuration ReadConfiguration(string json)
		{
			return JsonConvert.DeserializeObject<Configuration>(json);
		}

		public static string WriteConfiguration(Configuration configuration)
		{
			return JsonConvert.SerializeObject(configuration, Formatting.Indented);
		}

		public static string _ext_ = ".gk.json";
		public static string ConfigurationFolderPath
			=> Path.Combine(
				Environment.GetFolderPath(
					Environment.SpecialFolder.LocalApplicationData)
				, "GitKeeper");

		public static IEnumerable<Configuration> EnumConfigurations()
		{
			return EnumConfigurations(ConfigurationFolderPath);
		}

		public static IEnumerable<Configuration> EnumConfigurations(string path)
		{
			return Directory.EnumerateFiles(path, $"*{_ext_}").Select(p => new Configuration { path = p });
		}

		public static Configuration SaveConfiguration(string name, Configuration configuration)
		{
			return SaveConfiguration(name, configuration, ConfigurationFolderPath);
		}

		public static Configuration SaveConfiguration(string name, Configuration configuration, string path)
		{
			configuration.path = Path.Combine(path, name) + _ext_;
			File.WriteAllText(configuration.path, WriteConfiguration(configuration));
			return configuration;
		}
	}
}
