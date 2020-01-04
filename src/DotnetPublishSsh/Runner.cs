using System;
using Renci.SshNet;

namespace DotnetPublishSsh
{
	internal class Runner
	{
		public char DirectorySeparator { get; set; } = '/';

		private readonly ConnectionInfo connectionInfo;
		private readonly string cmdBefore;
		private readonly string cmdAfter;

		public Runner(PublishSshOptions publishSshOptions)
		{
			connectionInfo = Uploader.CreateConnectionInfo(publishSshOptions);
			cmdBefore = publishSshOptions.CmdBefore;
			cmdAfter = publishSshOptions.CmdAfter;
		}

		internal void RunBefore()
		{
			if (string.IsNullOrWhiteSpace(cmdBefore)) return;

			Console.WriteLine("Try to call before command: " + cmdBefore);
			using (SshClient client = new SshClient(connectionInfo))
			{
				client.Connect();
				SshCommand cmd = client.RunCommand(cmdBefore);
				Console.WriteLine(cmd.Result);
			}
		}

		internal void RunAfter()
		{
			if (string.IsNullOrWhiteSpace(cmdAfter)) return;

			Console.WriteLine("Try to call after command: " + cmdAfter);
			using (SshClient client = new SshClient(connectionInfo))
			{
				client.Connect();
				SshCommand cmd = client.RunCommand(cmdAfter);
				Console.WriteLine(cmd.Result);
			}
		}
	}
}