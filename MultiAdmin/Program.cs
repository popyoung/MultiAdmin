using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;

namespace MultiAdmin
{
	public static class Program
	{
		public const string MaVersion = "3.1.0";

		private static readonly List<Server> InstantiatedServers = new List<Server>();

		#region Server Properties

		public static Server[] Servers => ServerDirectories.Select(serverDir => new Server(Path.GetFileName(serverDir), serverDir)).ToArray();

		public static string[] ServerDirectories
		{
			get
			{
				string globalServersFolder = MultiAdminConfig.GlobalServersFolder;
				return !Directory.Exists(globalServersFolder) ? new string[] { } : Directory.GetDirectories(globalServersFolder);
			}
		}

		public static string[] ServerIds => Servers.Select(server => server.serverId).ToArray();

		#endregion

		#region Auto-Start Server Properties

		public static Server[] AutoStartServers => Servers.Where(server => !server.ServerConfig.ManualStart).ToArray();

		public static string[] AutoStartServerDirectories => AutoStartServers.Select(autoStartServer => autoStartServer.serverDir).ToArray();

		public static string[] AutoStartServerIds => AutoStartServers.Select(autoStartServer => autoStartServer.serverId).ToArray();

		#endregion

		public static bool Headless { get; private set; }

		public static void Write(string message, ConsoleColor color = ConsoleColor.DarkYellow)
		{
			lock (ColoredConsole.WriteLock)
			{
				if (Headless) return;

				ClearConsoleLine(new ColoredMessage(Utils.TimeStampMessage(message), color)).WriteLine();
			}
		}

		#region Clear Console Line Methods

		public static void ClearConsoleLine(int index, bool returnCursorPos = false)
		{
			lock (ColoredConsole.WriteLock)
			{
				if (Headless) return;

				int lastCursor = 0;
				try
				{
					lastCursor = returnCursorPos ? Console.CursorLeft : Console.WindowLeft;
				}
				catch
				{
					// ignored
				}

				try
				{
					Console.CursorLeft = index > Console.WindowWidth || index < Console.WindowWidth ? Console.WindowLeft : index;
					Console.Write(new string(' ', Console.WindowWidth - Console.CursorLeft - 1));
				}
				catch
				{
					// ignored
				}

				try
				{
					Console.CursorLeft = lastCursor;
				}
				catch
				{
					// ignored
				}
			}
		}

		public static string ClearConsoleLine(string message)
		{
			if (!string.IsNullOrEmpty(message))
				ClearConsoleLine(message.Contains(Environment.NewLine) ? 0 : message.Length);
			else
				ClearConsoleLine(0);

			return message;
		}

		public static ColoredMessage ClearConsoleLine(ColoredMessage message)
		{
			ClearConsoleLine(message?.text);
			return message;
		}

		public static ColoredMessage[] ClearConsoleLine(ColoredMessage[] message)
		{
			ClearConsoleLine(message?.GetText());
			return message;
		}

		public static List<ColoredMessage> ClearConsoleLine(List<ColoredMessage> message)
		{
			ClearConsoleLine(message?.GetText());
			return message;
		}

		#endregion

		private static void OnExit(object sender, EventArgs e)
		{
			foreach (Server server in InstantiatedServers)
				try
				{
					while (server.IsRunning)
					{
						server.StopServer(true);
						Thread.Sleep(10);
					}
				}
				catch
				{
					// ignored
				}
		}

		public static void Main()
		{
			AppDomain.CurrentDomain.ProcessExit += OnExit;

			Headless = GetFlagFromArgs("headless", "h");

			string serverIdArg = GetParamFromArgs("server-id", "id");
			string configArg = GetParamFromArgs("config", "c");

			Server server = null;

			if (!string.IsNullOrEmpty(serverIdArg) || !string.IsNullOrEmpty(configArg))
			{
				server = new Server(serverIdArg, configArg);

				InstantiatedServers.Add(server);
			}
			else
			{
				if (Servers.Length <= 0)
				{
					server = new Server();

					InstantiatedServers.Add(server);
				}
				else
				{
					Server[] autoStartServers = AutoStartServers;

					if (autoStartServers.Length <= 0)
					{
						Write("No servers are set to automatically start, please enter a Server ID to start:");
						InputThread.InputPrefix?.Write();

						server = new Server(Console.ReadLine());

						InstantiatedServers.Add(server);
					}
					else
					{
						Write("Starting this instance in multi server mode...");

						for (int i = 0; i < autoStartServers.Length; i++)
							if (i == 0)
							{
								server = autoStartServers[i];

								InstantiatedServers.Add(server);
							}
							else
							{
								StartServer(autoStartServers[i]);
							}
					}
				}
			}

			if (server != null)
			{
				if (!string.IsNullOrEmpty(server.serverId) && !string.IsNullOrEmpty(server.configLocation))
					Write($"Starting this instance with Server ID: \"{server.serverId}\" and config directory: \"{server.configLocation}\"...");

				else if (!string.IsNullOrEmpty(server.serverId))
					Write($"Starting this instance with Server ID: \"{server.serverId}\"...");

				else if (!string.IsNullOrEmpty(server.configLocation))
					Write($"Starting this instance with config directory: \"{server.configLocation}\"...");

				else
					Write("Starting this instance in single server mode...");

				server.StartServer();
			}
		}

		private static bool ArrayIsNullOrEmpty(ICollection<object> array)
		{
			return array == null || array.Count <= 0;
		}

		public static string GetParamFromArgs(string[] keys = null, string[] aliases = null)
		{
			if (ArrayIsNullOrEmpty(keys) && ArrayIsNullOrEmpty(aliases)) return null;

			string[] args = Environment.GetCommandLineArgs();

			for (int i = 0; i < args.Length - 1; i++)
			{
				string lowArg = args[i]?.ToLower();

				if (string.IsNullOrEmpty(lowArg)) continue;

				if (!ArrayIsNullOrEmpty(keys))
				{
					if (keys.Any(key => !string.IsNullOrEmpty(key) && lowArg == $"--{key.ToLower()}"))
					{
						return args[i + 1];
					}
				}

				if (!ArrayIsNullOrEmpty(aliases))
				{
					if (aliases.Any(alias => !string.IsNullOrEmpty(alias) && lowArg == $"-{alias.ToLower()}"))
					{
						return args[i + 1];
					}
				}
			}

			return null;
		}

		public static bool ArgsContainsParam(string[] keys = null, string[] aliases = null)
		{
			foreach (string arg in Environment.GetCommandLineArgs())
			{
				string lowArg = arg?.ToLower();

				if (string.IsNullOrEmpty(lowArg)) continue;

				if (!ArrayIsNullOrEmpty(keys))
				{
					if (keys.Any(key => !string.IsNullOrEmpty(key) && lowArg == $"--{key.ToLower()}"))
					{
						return true;
					}
				}

				if (!ArrayIsNullOrEmpty(aliases))
				{
					if (aliases.Any(alias => !string.IsNullOrEmpty(alias) && lowArg == $"-{alias.ToLower()}"))
					{
						return true;
					}
				}
			}

			return false;
		}

		public static bool GetFlagFromArgs(string[] keys = null, string[] aliases = null)
		{
			if (ArrayIsNullOrEmpty(keys) && ArrayIsNullOrEmpty(aliases)) return false;

			return bool.TryParse(GetParamFromArgs(keys, aliases), out bool result) ? result : ArgsContainsParam(keys, aliases);
		}

		public static string GetParamFromArgs(string key = null, string alias = null)
		{
			return GetParamFromArgs(new string[] {key}, new string[] {alias});
		}

		public static bool ArgsContainsParam(string key = null, string alias = null)
		{
			return ArgsContainsParam(new string[] {key}, new string[] {alias});
		}

		public static bool GetFlagFromArgs(string key = null, string alias = null)
		{
			return GetFlagFromArgs(new string[] {key}, new string[] {alias});
		}

		public static void StartServer(Server server)
		{
			string assemblyLocation = Assembly.GetEntryAssembly().Location;

			List<string> args = new List<string>();

			if (!string.IsNullOrEmpty(server.serverId))
				args.Add($"-id \"{server.serverId}\"");

			if (!string.IsNullOrEmpty(server.configLocation))
				args.Add($"-c \"{server.configLocation}\"");

			if (Headless)
				args.Add("-h");

			args.RemoveAll(string.IsNullOrEmpty);

			string stringArgs = string.Join(" ", args);

			ProcessStartInfo startInfo = new ProcessStartInfo(assemblyLocation, stringArgs);

			Write($"Launching \"{startInfo.FileName}\" with arguments \"{startInfo.Arguments}\"...");

			Process.Start(startInfo);
		}
	}
}