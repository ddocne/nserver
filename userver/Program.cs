using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Threading;
using System.Net;
using System.Net.Sockets;

namespace userver {
	class Program {
		IPAddress[] iAddr;
		int iPort;
		IPAddress[] sAddr;
		int sPort;
		IPAddress[] lAddr;
		int lPort;
		IPAddress[] rAddr;
		int rPort;
		int nCount = 1;
		string proto = "udp";

		static void Main (string[] args)
		{
			var p = new Program(args);
		}

		public Program (string[] args)
		{
			for (int i = 0; i < args.Length; ++i) {
				if (args[i][0] == '-') {
					var opt = args[i].TrimStart('-');

					if (opt.Equals("i")) {
						iAddr = Dns.GetHostAddresses(args[++i]);
						iPort = int.Parse(args[++i]);
					}

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

			var iep = new IPEndPoint(iAddr[0], iPort);

			List<Task> taskList = new List<Task>();

			if (lAddr != null) {
				foreach (var addr in lAddr) {
					var ep = new IPEndPoint(addr, lPort);
					if (proto.Equals("udp")) {
						taskList.Add(Task.Run(() => UdpServer(iep, ep)));
					} else {
						taskList.Add(Task.Run(() => TcpServer(ep)));
					}
				}
			}

			Thread.Sleep(1000);

			if (rAddr != null) {
				foreach (var addr in rAddr) {
					var ep = new IPEndPoint(addr, rPort);
					for (int i = 0; i < nCount; ++i) {
						if (proto.Equals("udp")) {
							taskList.Add(Task.Run(() => UdpClient(iep, ep)));
						} else {
							taskList.Add(Task.Run(() => TcpClient(ep)));
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


		void UdpServer (IPEndPoint iep, IPEndPoint lep)
		{
			using(var s = new Socket(lep.AddressFamily, SocketType.Dgram, ProtocolType.Udp)) {
				s.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
				s.Bind(lep);

				Console.WriteLine("s bind:" + lep.ToString());

				EndPoint rep = new IPEndPoint(IPAddress.Any, 0);
				byte[] buffer = new byte[1024];

				List<Socket> list = new List<Socket>();
				List<Socket> rlist = new List<Socket>();
				List<Socket> wlist = new List<Socket>();
				List<Socket> elist = new List<Socket>();

				Dictionary<EndPoint, Socket> dict = new Dictionary<EndPoint, Socket>();

				var listen = s;

				while (true) {
					rlist.Clear();
					if (listen != null) {
						rlist.Add(listen);
					}
					rlist.AddRange(list);
					Socket.Select(rlist, null, null, 1000);
					foreach (var sock in rlist) {
						if (sock == listen) {
							var len = sock.ReceiveFrom(buffer, 1024, SocketFlags.None, ref rep);
							Socket a;
							if (!dict.TryGetValue(rep, out a)) {
								a = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
								a.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
								a.Bind(iep);
								a.Connect(rep);
								dict[rep] = a;
								//Console.WriteLine("accept:" + iep.ToString() + " " + rep.ToString());
								Console.WriteLine("accept:" + a.LocalEndPoint.ToString() + " " + rep.ToString());
								list.Add(a);
								if(list.Count == nCount) {
									//Console.WriteLine("close:" + lep.ToString());
									//listen.Close();
									//listen = null;
								}
							}
							a.Send(buffer, len, SocketFlags.None);
						} else {
							var len = sock.Receive(buffer, 1024, SocketFlags.None);
							sock.Send(buffer, len, SocketFlags.None);
						}
					}
				}
			}
		}

		void UdpClient (IPEndPoint iep, IPEndPoint rep)
		{
			using (var s = new Socket(rep.AddressFamily, SocketType.Dgram, ProtocolType.Udp)) {
				s.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
				//s.Connect(rep);

				s.Blocking = false;

				byte[] buffer = new byte[1024];
				bool connected = false;

				s.SendTo(buffer, 1, SocketFlags.None, rep);
				Thread.Sleep(1000);

				while (true) {
					try {
						int len;
						if (connected) {
							len = s.Receive(buffer, buffer.Length, SocketFlags.None);
							if (len > 0) {
								Console.WriteLine("c1 recv:" + s.LocalEndPoint.ToString() + " " + iep.ToString());
							}
						} else {
							EndPoint ep = new IPEndPoint(IPAddress.Any, 0);
							len = s.ReceiveFrom(buffer, buffer.Length, SocketFlags.None, ref ep);
							s.Connect(ep);
							Console.WriteLine("c conn:" + s.LocalEndPoint.ToString() + " " + ep.ToString());
							if (len > 0) {
								Console.WriteLine("c2 recv:" + s.LocalEndPoint.ToString() + " " + ep.ToString());
							}
							connected = true;
						}
					} catch {

					}

					Thread.Sleep(1000);
					if (connected) {
						s.Send(buffer, 1, SocketFlags.None);
						//Console.WriteLine("send:" + s.LocalEndPoint.ToString() + " " + iep.ToString());
					} else {
						s.SendTo(buffer, 1, SocketFlags.None, rep);
						Console.WriteLine("send:" + s.LocalEndPoint.ToString() + " " + rep.ToString());
					}
				}
			}
		}

		void TcpServer (IPEndPoint lep)
		{
		}

		void TcpClient (IPEndPoint lep)
		{
		}


	}
}
