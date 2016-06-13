using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Security.Permissions;
using System.Text;
using System.Threading;

namespace FileClient
{
	class Program
	{
		#region socket
		private static int Port = 9999;  // read from config file
		private static bool IsConnectedToServer = false; // how many pending connection exist
		private static Socket ClientSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
		private static string IP = "127.0.0.1";
		private static string ServerPublicKey = "";

		private static void GetLocalIPAddress()
		{
			var host = Dns.GetHostEntry(Dns.GetHostName());
			foreach (var ip in host.AddressList)
			{
				if (ip.AddressFamily == AddressFamily.InterNetwork)
				{
					IP = ip.ToString();
					break;
				}
			}
		}

		#region encrypt

		private static byte[] Buffer = new byte[1024];
		public static string Encrypt(string text)
		{
			string encryptText = text;
			if (ServerPublicKey.Length > 0)
			{
				RSACryptoServiceProvider rsaPublic = new RSACryptoServiceProvider();
				rsaPublic.FromXmlString(ServerPublicKey);

				byte[] encryptedRSA = rsaPublic.Encrypt(Encoding.ASCII.GetBytes(text), false);
				encryptText = Convert.ToBase64String(encryptedRSA);
			}
			return encryptText;
		}

		#endregion

		private static void ReceiveCallback(IAsyncResult AR)
		{
			try
			{
				Socket socket = (Socket)AR.AsyncState;
				int received = socket.EndReceive(AR);
				byte[] buffer = new byte[received];
				Array.Copy(Buffer, buffer, received);
				ServerPublicKey = Encoding.ASCII.GetString(buffer);
			}
			catch (Exception e)
			{
				Console.WriteLine("Remote socket connect exception: " + e.ToString());
			}
		}

		private static void ConnectToServer()
		{
			try
			{
				string serverIp = IP;
				String[] arguments = Environment.GetCommandLineArgs();
				if(arguments.Length > 1)
				{
					serverIp = arguments[1];
				}
				ClientSocket.Connect(serverIp, Port);
				IsConnectedToServer = true;
				Console.WriteLine("Connected to server {0} success!", serverIp);

				// get public key from server
				byte[] buffer = Encoding.ASCII.GetBytes("hello, please give me public key!");
				int bytesTransferred = ClientSocket.Send(buffer);
				ClientSocket.BeginReceive(Buffer, 0, Buffer.Length, SocketFlags.None, ReceiveCallback, ClientSocket);
			}
			catch (SocketException e)
			{
				// currently do nothing
				IsConnectedToServer = false;
				Console.WriteLine("Connect to server {0} fail! error info:" + e.ToString());
			}
		}

		private static void SendToServer(string text)
		{
			try
			{
				String timeStamp = DateTime.Now.ToString("MM/dd/yyyy HH:mm:ss ");
				byte[] buffer = Encoding.ASCII.GetBytes(Encrypt(timeStamp + " ip:" + IP + " " + text));
				int bytesTransferred = ClientSocket.Send(buffer);
				Console.WriteLine("send text to server: " + text);
			}
			catch (Exception e)
			{
				// currently do nothing
				Console.WriteLine("Sending info to server exception! error info: " + e.ToString());
			}
		}

		private static void DisconnectToServer()
		{
			// Release the socket.
			if (IsConnectedToServer)
			{
				try
				{
					ClientSocket.Shutdown(SocketShutdown.Both);
					ClientSocket.Disconnect(true);
					ClientSocket.Close();
					Console.WriteLine("socket closed!");
				}
				catch (Exception e)
				{
					Console.WriteLine("Connect to server exception! error info: " + e.ToString());
				}
			}
		}
		#endregion socket

		#region file
		#region event
		// Define the event handlers.
		private static void OnChanged(object source, FileSystemEventArgs evt)
		{
			// Specify what is done when a file is changed, created, or deleted.
			if (IsConnectedToServer)
			{
				if (evt.ChangeType != WatcherChangeTypes.Deleted)
				{
					string text = "";
					try
					{
						text = "File: " + evt.FullPath + "   filesize: " + new FileInfo(evt.FullPath).Length + "bytes";
					}
					catch (Exception e)
					{
						Console.WriteLine("Reading file exception! error info: " + e.ToString());
					}
					if (text.Length > 0)
					{
						SendToServer(text);
					}
				}
			}
		}
		#endregion

		[PermissionSet(SecurityAction.Demand, Name = "FullTrust")]
		private static void Run()
		{
			GetLocalIPAddress();
			// try to connect server
			ConnectToServer();

			//by default monitor all drives
			string[] drivers = Environment.GetLogicalDrives();
			FileSystemWatcher[] watchers = new FileSystemWatcher[drivers.Length];
			int i = 0;
			foreach (string drive in drivers)
			{
				FileSystemWatcher watcher = new FileSystemWatcher();
				try
				{
					watcher.Path = drive;
				}
				catch(Exception e)
				{
					// this means current drive is not avaiable to watch
					Console.WriteLine("Watcher path error: " + e.ToString());
					continue;
				}
				watcher.Changed += new FileSystemEventHandler(OnChanged);
				watcher.Created += new FileSystemEventHandler(OnChanged);
				//watcher.Deleted += new FileSystemEventHandler(FolderWatcherTest_Deleted);
				watcher.NotifyFilter = NotifyFilters.FileName | NotifyFilters.Size;
				watcher.Filter = "*.txt";
				watchers[i] = watcher;
				//Begin watching.
				watcher.EnableRaisingEvents = true;
				watcher.IncludeSubdirectories = true;
				i++;
			}

			// Wait for the user to quit the program.
			Console.WriteLine("You can modify any txt files or press \'q\' to quit.");
			while (Console.Read() != 'q')
			{
				DisconnectToServer();
			}

		}
		#endregion

		public static void Main()
		{
			Console.Title = "Client";
			Run();
		}
	}
}
