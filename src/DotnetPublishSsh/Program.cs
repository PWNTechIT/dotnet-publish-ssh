using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace DotnetPublishSsh
{
	internal sealed class Program
	{
		private static HashSet<string> ChangedFiles = new HashSet<string>();
		private static void OnChanged(object source, FileSystemEventArgs e)
		{
			if (e.ChangeType == WatcherChangeTypes.Deleted)
				ChangedFiles.RemoveWhere(x => x == e.FullPath);
			else
				ChangedFiles.Add(e.FullPath);
		}

		private static void OnRenamed(object source, RenamedEventArgs e) => ChangedFiles.Add(e.FullPath);

		private static DateTime InvocationTime;

		//[PermissionSet(SecurityAction.Demand, Name = "FullTrust")]
		public static void Main(string[] args)
		{
			InvocationTime = DateTime.UtcNow;
			Console.WriteLine($"Processor Count: {Environment.ProcessorCount}");

			var options = PublishSshOptions.ParseArgs(args);
			if (options.PrintHelp)
			{
				PrintHelp();
				return;
			}

			PrepareOptions(options);

			var arguments = string.Join(" ", options.Args);
			var localPath = Path.GetFullPath(options.LocalPath);
			Directory.CreateDirectory(localPath);

			using (FileSystemWatcher watcher = new FileSystemWatcher())
			{
				watcher.Path = localPath;
				watcher.NotifyFilter = NotifyFilters.LastAccess
									 | NotifyFilters.LastWrite
									 | NotifyFilters.FileName
									 | NotifyFilters.DirectoryName;

				// Add event handlers.
				watcher.Changed += OnChanged;
				watcher.Created += OnChanged;
				watcher.Deleted += OnChanged;
				watcher.Renamed += OnRenamed;

				// Begin watching.
				watcher.EnableRaisingEvents = true;

				// Wait for the user to quit the program.
				if (!PublishLocal(arguments))
					return;
			}

			var path = options.Path;
			if (!path.EndsWith("/")) path = path + "/";
			localPath += Path.DirectorySeparatorChar;

			var localFiles = ChangedFiles; // GetLocalFiles(localPath);

			if (localFiles.Count <= 0)
			{
				Console.WriteLine($"No files needs to be uploaded!");
			}
			else
			{
				Console.WriteLine($"\nUploading {localFiles.Count} files to {options.User}@{options.Host}:{options.Port}{options.Path}");

				try
				{
					var runner = new Runner(options);
					runner.RunBefore();
					var uploader = new Uploader(options);
					uploader.UploadFiles(path, localFiles.Select(f => new LocalFile(localPath, f)).ToList());
					runner.RunAfter();
				}
				catch (Exception ex)
				{
					Console.WriteLine($"Error uploading files to server: {ex.Message}");
				}
			}
			//Directory.Delete(localPath, true);
			Console.WriteLine($"\nPublished in {TimeSpan.FromSeconds((int)(DateTime.UtcNow - InvocationTime).TotalSeconds):g} !\nThanks for using dotnet publish-ssh!");
		}

		private static void PrepareOptions(PublishSshOptions options)
		{
			if (string.IsNullOrEmpty(options.LocalPath))
			{
				var tempPath = Path.Combine(Path.GetTempPath(), $"publish.{Guid.NewGuid()}");
				Directory.CreateDirectory(tempPath);
				options.LocalPath = tempPath;
			}

			options.Args = options.Args.Concat(new[] { "-o", options.LocalPath }).ToArray();
		}

		private static bool PublishLocal(string arguments)
		{
			Console.WriteLine($"Starting `dotnet {arguments}`");

			var info = new ProcessStartInfo
			{
				FileName = "dotnet",
				Arguments = "publish " + arguments
			};

			var process = Process.Start(info);
			process.WaitForExit();
			var exitCode = process.ExitCode;

			Console.WriteLine($"dotnet publish exited with code {exitCode}");

			return exitCode == 0;
		}

		private static List<LocalFile> GetLocalFiles(string localPath)
		{
			var localFiles = Directory
				.EnumerateFiles(localPath, "*.*", SearchOption.AllDirectories)
				.Select(f => new LocalFile(localPath, f))
				.ToList();
			return localFiles;
		}

		private static void PrintHelp()
		{
			Console.WriteLine("Publish to remote server via SSH");
			Console.WriteLine();
			Console.WriteLine("Usage: dotnet publish-ssh [arguments] [options]");
			Console.WriteLine();
			Console.WriteLine("Arguments and options are the same as for `dotnet publish`");
			Console.WriteLine();
			Console.WriteLine("SSH specific options:");
			Console.WriteLine("  --ssh-host *              Host address");
			Console.WriteLine("  --ssh-port                Host port");
			Console.WriteLine("  --ssh-user *              User name");
			Console.WriteLine("  --ssh-password            Password");
			Console.WriteLine("  --ssh-keyfile             Private OpenSSH key file");
			Console.WriteLine("  --ssh-path *              Publish path on remote server");
			Console.WriteLine("  --ssh-cmd-before *        Run command before publish");
			Console.WriteLine("  --ssh-cmd-after *         Run command file after publish");
			Console.WriteLine();
		}
	}
}