using System;
using System.IO;

namespace DotnetPublishSsh
{
	internal sealed class LocalFile
	{
		public LocalFile(string localPath, string fileName)
		{
			FileName = fileName;
			RelativeName = new Uri(localPath).MakeRelativeUri(new Uri(fileName)).OriginalString;
			Info = new FileInfo(localPath);
		}

		public string FileName { get; set; }
		public string RelativeName { get; set; }

		public FileInfo Info { get; }
		public DateTime LastWriteTime => Info?.LastWriteTimeUtc ?? DateTime.MinValue;

		public bool IsNewer(DateTime lastUploadedTime) => LastWriteTime > lastUploadedTime;
	}
}