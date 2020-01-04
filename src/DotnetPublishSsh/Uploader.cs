using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Renci.SshNet;

namespace DotnetPublishSsh
{
	internal sealed class Uploader
	{
		public char DirectorySeparator { get; set; } = '/';

		private readonly ConnectionInfo _connectionInfo;
		private readonly HashSet<string> _existingDirectories = new HashSet<string>();

		public Uploader(PublishSshOptions publishSshOptions)
		{
			_connectionInfo = CreateConnectionInfo(publishSshOptions);
		}

		public static ConnectionInfo CreateConnectionInfo(PublishSshOptions options)
		{
			var authenticationMethods = new List<AuthenticationMethod>();

			if (options.Password != null)
				authenticationMethods.Add(
					new PasswordAuthenticationMethod(options.User, options.Password));

			if (options.KeyFile != null)
				authenticationMethods.Add(
					new PrivateKeyAuthenticationMethod(options.User, new PrivateKeyFile(options.KeyFile)));

			var connectionInfo = new ConnectionInfo(
				options.Host,
				options.Port,
				options.User,
				authenticationMethods.ToArray());

			return connectionInfo;
		}

		public void UploadFiles(string path, ICollection<LocalFile> localFiles)
		{
			//using (var client = new SshClient(_connectionInfo))
			using (var ftp = new SftpClient(_connectionInfo))
			{
				//var attrs = ftp.GetAttributes(path);
				//client.Connect();
				ftp.Connect();
				//var i = 1;
				//var tot = localFiles.Count;

				Parallel.ForEach(
					localFiles,
					new ParallelOptions { MaxDegreeOfParallelism = Environment.ProcessorCount },
					localFile => {
						UploadFile(localFile, ftp, path);
					}
				);

				//ftp.Create($"{path}{DirectorySeparator}.publish");
			}
			Console.WriteLine($"\nProcessed {localFiles.Count} files.");
		}

		private void UploadFile(LocalFile localFile, SftpClient ftp, string path)
		{
			//Console.WriteLine($"Checking: {localFile.RelativeName}");
			//Console.Write($"(local) {localFile.LastWriteTime.ToLocalTime()}");

			var filePath = localFile.RelativeName.Replace(Path.DirectorySeparatorChar, DirectorySeparator);
			var fullPath = path + filePath;

			// Check if the file exists
			var fileExists = ftp.Exists(fullPath);
			if (!fileExists) EnsureDirExists(ftp, fullPath);

			if (fileExists)
			{
				var lastUpdate = ftp.GetLastWriteTimeUtc(fullPath);
				//Console.Write($"| {lastUpdate.ToLocalTime()} (online)\n");

				if (!localFile.IsNewer(lastUpdate))
				{
					Console.WriteLine($"Not uploading because file {localFile.RelativeName} is already updated!");
					return;
				}
			}

			using (var stream = File.OpenRead(localFile.FileName))
			{
				//Console.WriteLine($"Uploading: {localFile.RelativeName}");
				ftp.UploadFile(stream, fullPath, true);
				Console.WriteLine($"Uploaded: {localFile.RelativeName}");
			}
		}

		private void EnsureDirExists(SftpClient ftp, string path)
		{
			var parts = path.Split(new[] { DirectorySeparator }, StringSplitOptions.RemoveEmptyEntries)
				.Where(p => !string.IsNullOrEmpty(p))
				.ToList();

			if (!path.EndsWith(DirectorySeparator.ToString()))
				parts = parts.Take(parts.Count - 1).ToList();

			CreateDir(ftp, parts);
		}

		private void CreateDir(SftpClient ftp, ICollection<string> parts, bool noCheck = false)
		{
			if (parts.Any())
			{
				var path = Combine(parts);
				var parent = parts.Take(parts.Count - 1).ToList();

				if (noCheck || ftp.Exists(path))
				{
					CreateDir(ftp, parent, true);
				}
				else
				{
					CreateDir(ftp, parent);
					ftp.CreateDirectory(path);
				}

				_existingDirectories.Add(path);
			}
		}

		private string Combine(ICollection<string> parts)
		{
			var path = DirectorySeparator +
					   string.Join(DirectorySeparator.ToString(), parts) +
					   (parts.Any() ? DirectorySeparator.ToString() : "");
			return path;
		}
	}
}