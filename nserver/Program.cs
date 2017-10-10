using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Net;
using System.Net.Sockets;

namespace nserver {
	class Program {
		IPAddress[] sAddr;
		int sPort;
		IPAddress[] lAddr;
		int lPort;
		IPAddress[] rAddr;
		int rPort;
		int nCount = 1;
		string proto = "udp";

		public Program (string[] args)
		{
			for (int i = 0; i < args.Length; ++i) {
				if (args[i][0] == '-') {
					var opt = args[i].TrimStart('-');
					if (opt.Equals("r")) {
						rAddr = Dns.GetHostAddresses(args[++i]);
						rPort = int.Parse(args[++i]);
					}

					if (opt.Equals("l")) {
						lAddr = Dns.GetHostAddresses(args[++i]);
						lPort = int.Parse(args[++i]);
					}

					if (opt.Equals("s")) {
						sAddr = Dns.GetHostAddresses(args[++i]);
						sPort = int.Parse(args[++i]);
					}

					if (opt.Equals("n")) {
						nCount = int.Parse(args[++i]);
					}

					if (opt.Equals("proto")) {
						proto = args[++i];
					}
				}
			}

			List<Task> taskList = new List<Task>();
			if (lAddr != null) {
				foreach (var addr in lAddr) {
					if (proto.Equals("udp")) {
						taskList.Add(Server(new IPEndPoint(addr, lPort)));
					} else {
						taskList.Add(TcpServer(new IPEndPoint(addr, lPort)));
					}
				}
			}

			if(rAddr != null) {
				foreach (var addr in rAddr) {
					for (int i = 0; i < nCount; ++i) {
						if (proto.Equals("udp")) {
							taskList.Add(Client(new IPEndPoint(addr, rPort)));
						} else {
							taskList.Add(TcpClient(new IPEndPoint(addr, rPort)));
						}
					}
				}
			}
			var ta = taskList.ToArray();

			while (true) {
				if (Task.WaitAll(ta, 1000)) {
					break;
				}			
			}
		}

		public Task TcpServer (EndPoint ep)
		{
			return Task.Run(() => {
				using (var s = new Socket(ep.AddressFamily, SocketType.Stream, ProtocolType.Tcp)) {
					s.Bind(ep);
					s.Listen(5);

					Console.WriteLine("tcp server:" + s.LocalEndPoint.ToString());

					byte[] buffer = new byte[1024 * 4];

					Dictionary<EndPoint, Socket> dict = new Dictionary<EndPoint, Socket>();

					SocketAsyncEventArgs e = new SocketAsyncEventArgs();
					//e.RemoteEndPoint = new IPEndPoint(IPAddress.Any, 0);
					e.Completed += (sender, args) => {
						Socket a;
						var rep = e.AcceptSocket.RemoteEndPoint;
						if (!dict.TryGetValue(rep, out a)) {
							dict[rep] = s;
							TcpRemote(ep, rep, e.AcceptSocket);
						} else {
							Console.WriteLine("remote " + rep.ToString());
						}
						if (dict.Count == nCount) {
							//s.Close();
							//Console.WriteLine("close");
						} else {
							//s.ReceiveFromAsync(e);
						}
						e.AcceptSocket = null;
						s.AcceptAsync(e);
						//s.Close();
					};
					e.AcceptSocket = null;
					s.AcceptAsync(e);

					//EndPoint rep = new IPEndPoint(IPAddress.Any, 0);
					//s.BeginReceiveFrom(buffer, 0, 1024 * 4, SocketFlags.None, ref rep, ar => {
					//	var len = s.EndReceiveFrom(ar, ref rep);
					//	Socket a;
					//	if (!dict.TryGetValue(rep, out a)) {
					//		dict[e.RemoteEndPoint] = s;
					//		Remote(ep, rep, buffer, len);
					//	}
					//	//s.Close();
					//}, s);
					//s.ReceiveFromAsync(e);

					//EndPoint rep = new IPEndPoint(IPAddress.Any, 0);

					while (true) {
						System.Threading.Thread.Sleep(1000);
					}
				}
			});
		}

		public Task TcpRemote (EndPoint lep, EndPoint rep, Socket s)
		{
			return Task.Run(() => {
				Console.WriteLine("accept:" + s.LocalEndPoint.ToString() + ":" + s.RemoteEndPoint.ToString());
				byte[] buffer = new byte[1024 * 4];

				SocketAsyncEventArgs e = new SocketAsyncEventArgs();
				e.SetBuffer(new byte[1024 * 4], 0, 1024 * 4);
				e.RemoteEndPoint = rep;
				e.Completed += RemoteRecv_Completed;
				if (!s.ReceiveAsync(e)) {
					Console.WriteLine("error");
				}

				while (true) {
					//s.Send(System.Text.Encoding.UTF8.GetBytes("test"));
					System.Threading.Thread.Sleep(1000);
				}
			});
		}

		public Task Server (EndPoint ep)
		{
			return Task.Run(() => {
				using (var s = new Socket(ep.AddressFamily, SocketType.Dgram, ProtocolType.Udp)) {
					s.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
					s.Bind(ep);

					Console.WriteLine("server:" + s.LocalEndPoint.ToString());

					byte[] buffer = new byte[1024 * 4];

					Dictionary<EndPoint, Socket> dict = new Dictionary<EndPoint, Socket>();

					SocketAsyncEventArgs e = new SocketAsyncEventArgs();
					e.SetBuffer(buffer, 0, 1024 * 4);
					e.RemoteEndPoint = new IPEndPoint(IPAddress.Any, 0);
					e.Completed += (sender, args) => {
						Console.WriteLine("server:recv");
						var len = e.BytesTransferred;
						Socket a;

						if (!dict.TryGetValue(e.RemoteEndPoint, out a)) {
							dict[e.RemoteEndPoint] = s;
							var data = new byte[len];
							System.Array.Copy(buffer, data, len);
							Remote(ep, e.RemoteEndPoint, data, len);
						} else {
							var text = System.Text.Encoding.UTF8.GetString(e.Buffer, 0, e.BytesTransferred);
							Console.WriteLine("remote " + e.RemoteEndPoint.ToString() + " recv:" + text);
						}
						if (dict.Count == nCount) {
							//s.Shutdown(SocketShutdown.Both);
							s.Close();
							Console.WriteLine("server:close");
						} else {
							e.RemoteEndPoint = new IPEndPoint(IPAddress.Any, 0);
							s.ReceiveFromAsync(e);
							Console.WriteLine("server:ReceiveFromAsync");
						}
						//s.ReceiveFromAsync(e);
						//s.Close();
					};
					s.ReceiveFromAsync(e);

					while (true) {
						System.Threading.Thread.Sleep(1000);
					}
				}
			});
		}

		public Task Remote (EndPoint lep, EndPoint rep, byte[] data, int dataSize)
		{
			return Task.Run(() => {
				using (var s = new Socket(lep.AddressFamily, SocketType.Dgram, ProtocolType.Udp)) {
					s.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
					s.Bind(lep);
					s.Connect(rep);

					string text = System.Text.Encoding.UTF8.GetString(data, 0, dataSize);
					Console.WriteLine("accept:" + s.LocalEndPoint.ToString() + ":" + s.RemoteEndPoint.ToString() + ":" + text);

					SocketAsyncEventArgs e = new SocketAsyncEventArgs();
					byte[] buffer = new byte[1024 * 4];
					e.SetBuffer(buffer, 0, 1024 * 4);
					e.RemoteEndPoint = rep;
					e.Completed += RemoteRecv_Completed;
					if (!s.ReceiveAsync(e)) {
						Console.WriteLine("error");
					}

					while (true) {
						//s.Send(System.Text.Encoding.UTF8.GetBytes("test"));
						System.Threading.Thread.Sleep(1000);
					}
				}
			});
		}

		private void RemoteRecv_Completed (object sender, SocketAsyncEventArgs e)
		{
			Socket s = (Socket)sender;
			var text = System.Text.Encoding.UTF8.GetString(e.Buffer, 0, e.BytesTransferred);
			//Console.WriteLine("rr " + e.RemoteEndPoint.ToString() + " recv:" + text);
			s.Send(e.Buffer, e.BytesTransferred, SocketFlags.None);
			s.ReceiveAsync(e);
		}

		public Task Client (EndPoint ep)
		{
			return Task.Run(() => {
				using (var s = new Socket(ep.AddressFamily, SocketType.Dgram, ProtocolType.Udp)) {
					s.Connect(ep);

					Console.WriteLine("client:" + s.LocalEndPoint.ToString() + ":" + s.RemoteEndPoint.ToString());
					int count = 0;

					SocketAsyncEventArgs e = new SocketAsyncEventArgs();
					e.SetBuffer(new byte[1024 * 4], 0, 1024 * 4);
					e.Completed += ClientRecv_Completed;
					e.RemoteEndPoint = new IPEndPoint(IPAddress.Any, 0);
					s.ReceiveAsync(e);

					while (true) {
						string line = string.Format("test:{0} {1}", count++, s.LocalEndPoint.ToString());
						Console.WriteLine(Thread.CurrentThread.ManagedThreadId + " client " + s.LocalEndPoint.ToString() + " send:" + line);
						var data = System.Text.Encoding.UTF8.GetBytes(line);
						s.Send(data);
						System.Threading.Thread.Sleep(1000);
					}
				}
			});
		}

		private void ClientRecv_Completed (object sender, SocketAsyncEventArgs e)
		{
			Socket s = (Socket)sender;
			string text = System.Text.Encoding.UTF8.GetString(e.Buffer, 0, e.BytesTransferred);
			Console.WriteLine(Thread.CurrentThread.ManagedThreadId + " client " + s.LocalEndPoint.ToString() + " recv:" + e.BytesTransferred + ":" + text);
			s.ReceiveAsync(e);
		}

		public Task TcpClient (EndPoint ep)
		{
			return Task.Run(() => {
				using (var s = new Socket(ep.AddressFamily, SocketType.Stream, ProtocolType.Tcp)) {
					s.Connect(ep);

					Console.WriteLine("client:" + s.LocalEndPoint.ToString() + ":" + s.RemoteEndPoint.ToString());
					int count = 0;

					SocketAsyncEventArgs e = new SocketAsyncEventArgs();
					e.SetBuffer(new byte[1024 * 4], 0, 1024 * 4);
					e.Completed += ClientRecv_Completed;
					e.RemoteEndPoint = new IPEndPoint(IPAddress.Any, 0);
					s.ReceiveAsync(e);

					while (true) {
						string line = string.Format("test:{0} {1}", count++, s.LocalEndPoint.ToString());
						Console.WriteLine(Thread.CurrentThread.ManagedThreadId + " client " + s.LocalEndPoint.ToString() + " send:" + line);
						var data = System.Text.Encoding.UTF8.GetBytes(line);
						s.Send(data);
						System.Threading.Thread.Sleep(1000);
					}
				}
			});
		}

		public class Buffer {
			public Socket s;
			public byte[] data;
			public Buffer (Socket _s, int size)
			{
				s = _s;
				data = new byte[size];
			}
		}

		public void Server (object o)
		{
			var s = (Socket)o;

			var buffer = new Buffer(s, 10240);
			EndPoint ep = new IPEndPoint(IPAddress.Any, 0);

			var async = s.BeginReceiveFrom(buffer.data, 0, 10240, SocketFlags.None, ref ep, asyncCallback, buffer);

			while (true) {
				int read = s.EndReceiveFrom(async, ref ep);
				if (read > 0) {
					string line = System.Text.Encoding.UTF8.GetString(buffer.data, 0, read);
					Console.WriteLine(ep.ToString() + ":" + line);
					s.BeginSendTo(buffer.data, 0, read, SocketFlags.None, ep, serverSendCallback, buffer);
					async = s.BeginReceiveFrom(buffer.data, 0, 10240, SocketFlags.None, ref ep, asyncCallback, buffer);
				}
				Thread.Sleep(1000);
			}
		}

		void serverSendCallback (IAsyncResult ar)
		{
			Console.WriteLine("serverSnedCallback");
		}

		void asyncCallback (IAsyncResult ar)
		{
			Buffer b = (Buffer)ar.AsyncState;
			Console.WriteLine("asyncCallback");
		}

		public void Client (object o)
		{
			var s = (Socket)o;

			var buffer = new Buffer(s, 10240);
			EndPoint ep = new IPEndPoint(IPAddress.Any, 0);

			var async = s.BeginReceiveFrom(buffer.data, 0, 10240, SocketFlags.None, ref ep, clientRecvCallback, buffer);

			while (true) {
				var line = Console.ReadLine();
				byte[] data = System.Text.Encoding.UTF8.GetBytes(line);
				s.BeginSend(data, 0, data.Length, SocketFlags.None, endClientSnedCallback, s);
			}
		}

		void clientRecvCallback (IAsyncResult ar)
		{
			EndPoint ep = new IPEndPoint(IPAddress.Any, 0);
			Buffer b = (Buffer)ar.AsyncState;
			int read = b.s.EndReceiveFrom(ar, ref ep);
			if (read > 0) {
				string line = System.Text.Encoding.UTF8.GetString(b.data, 0, read);
				Console.WriteLine(ep.ToString() + ":" + line);
			}
		}

		void endClientSnedCallback (IAsyncResult ar)
		{
			Console.WriteLine("endClientSnedCallback");
		}

		public void Execute()
		{

		}

		static void Main (string[] args)
		{
			var main = new Program(args);
			main.Execute();
		}
	}
}
