using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace server
{
	class Program
	{
		#region encryption
		private static string ContainerName = "EncryptServer";
		public static void GenKey_SaveInContainer(string containerName)
		{
			// Create the CspParameters object and set the key container
			CspParameters cp = new CspParameters();
			cp.KeyContainerName = containerName;

			// Create a new instance of RSACryptoServiceProvider
			RSACryptoServiceProvider rsa = new RSACryptoServiceProvider(cp);

			// Display the key information to the console.
			Console.WriteLine("Key added to container");
		}

		public static string GetPublicKeyFromContainer(string containerName)
		{
			// Create the CspParameters object and set the key container 
			// name used to store the RSA key pair.
			CspParameters cp = new CspParameters();
			cp.KeyContainerName = containerName;

			RSACryptoServiceProvider rsa = new RSACryptoServiceProvider(cp);
			string publicXml = rsa.ToXmlString(false);
			return publicXml;
		}


		public static string Decrypt(string containerName, string ecryptText)
		{
			//Decode with private key
			CspParameters cp1 = new CspParameters();
			cp1.KeyContainerName = containerName;
			RSACryptoServiceProvider rsa1 = new RSACryptoServiceProvider(cp1);
			rsa1.ImportParameters(rsa1.ExportParameters(true));
			byte[] decryptedRSA = rsa1.Decrypt(Convert.FromBase64String(ecryptText), false);
			string originalResult = Encoding.Default.GetString(decryptedRSA);
			return originalResult;
		}
		#endregion
		#region server
		#region logfile
		private static ReaderWriterLockSlim Lock = new ReaderWriterLockSlim();

		private static void log(string text)
		{
			//ensure atomic write
			Lock.EnterWriteLock();
			try
			{
				using (StreamWriter w = File.AppendText(@"C:\Users\Public\server.log"))
				{
					w.WriteLine(text);
				}
			}
			finally
			{
				Lock.ExitWriteLock();
			}
		}
		#endregion

		private static int Port = 9999;  // read from config file
		private static int NumBackLog = 5; // how many pending connection exist

		private static byte[] Buffer = new byte[1024];
		private static Socket ServerSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);

		private static void StartServer()
		{
			Console.Title = "Server";
			Console.WriteLine("Starting server...");
			try
			{
				ServerSocket.Bind(new IPEndPoint(IPAddress.Any, Port));
				ServerSocket.Listen(NumBackLog);
				ServerSocket.BeginAccept(new AsyncCallback(AcceptCallback), null);
			}
			catch (Exception e)
			{
				Console.WriteLine("Start server exception: "+e.ToString());
			}
			Console.WriteLine("server is running...");
		}

		private static void AcceptCallback(IAsyncResult AR)
		{
			Socket socket = ServerSocket.EndAccept(AR);
			socket.BeginReceive(Buffer, 0, Buffer.Length, SocketFlags.None, new AsyncCallback(ReceiveCallback), socket);
			ServerSocket.BeginAccept(new AsyncCallback(AcceptCallback), null);
		}

		private static void ReceiveCallback(IAsyncResult AR)
		{
			try
			{
				Socket socket = (Socket)AR.AsyncState;
				int received = socket.EndReceive(AR);
				byte[] buffer = new byte[received];
				Array.Copy(Buffer, buffer, received);
				string text = Encoding.ASCII.GetString(buffer);
				if(text == "hello, please give me public key!") // send
				{
					socket.Send(Encoding.ASCII.GetBytes(GetPublicKeyFromContainer(ContainerName)));
				}
				else
				{
					text = Decrypt(ContainerName, text);
					log(text);
				}
				socket.BeginReceive(Buffer, 0, Buffer.Length, SocketFlags.None, new AsyncCallback(ReceiveCallback), socket);
			}
			catch (Exception e)
			{
				Console.WriteLine("Remote socket connect exception: " + e.ToString());
			}

		}

		static void SendCallback(IAsyncResult AR)
		{
			//TODO: SendCallback
		}
		#endregion

		static void Main(string[] args)
		{
			// create public and private key pair
			GenKey_SaveInContainer(ContainerName);

			StartServer();
			Console.ReadLine();
		}
	}
}
