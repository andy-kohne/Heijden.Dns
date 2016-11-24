using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Heijden.DNS;

namespace Heijden.Dns.Portable
{
    public class VerboseEventArgs : EventArgs
    {
        public string Message { get; }
        public VerboseEventArgs(string message)
        {
            Message = message;
        }
    }

    /// <summary>
    /// Resolver is the main class to do DNS query lookups
    /// </summary>
    public class Resolver
	{
	    /// <summary>
		/// Default DNS port
		/// </summary>
		public const int DefaultPort = 53;

        /// <summary>
        /// OpenDNS dns servers.
        /// For information only.
        /// </summary>
        public static readonly IPEndPoint[] DefaultDnsServers =
            {
                new IPEndPoint(IPAddress.Parse("208.67.222.222"), DefaultPort),
                new IPEndPoint(IPAddress.Parse("208.67.220.220"), DefaultPort)
            };

        private ushort unique;
		private bool useCache;
	    private int retries;

	    private readonly List<IPEndPoint> dnsServers;
		private readonly Dictionary<string,Response> responseCache = new Dictionary<string, Response>();

        #region public properties
        /// <summary>
        /// Verbose messages from internal operations
        /// </summary>
        public event EventHandler<VerboseEventArgs> OnVerbose;


        public string Version => typeof(Resolver).GetTypeInfo().Assembly.GetName().Version.ToString();

        /// <summary>
        /// Gets first DNS server address or sets single DNS server to use
        /// </summary>
        public IPAddress DnsServer => dnsServers.FirstOrDefault()?.Address;

        public TimeSpan TimeOut { get; set; }

        /// <summary>
        /// Gets or set recursion for doing queries
        /// </summary>
        public bool Recursion { get; set; }

        /// <summary>
        /// Gets or sets protocol to use
        /// </summary>
        public TransportType TransportType { get; set; }

        /// <summary>
        /// Gets or sets number of retries before giving up
        /// </summary>
        public int Retries
        {
            get
            {
                return retries;
            }
            set
            {
                if (value >= 1)
                    retries = value;
            }
        }

        /// <summary>
        /// Gets or sets list of DNS servers to use
        /// </summary>
        public List<IPEndPoint> DnsServers
        {
            get
            {
                return dnsServers;
            }
            set
            {
                dnsServers.Clear();
                if(value != null)
                    dnsServers.AddRange(value);
            }
        }

        public bool UseCache
        {
            get
            {
                return useCache;
            }
            set
            {
                useCache = value;
                if (!useCache)
                    ClearCache();
            }
        }
        #endregion


        /// <summary>
        /// Resolver constructor, using DNS servers specified by Windows
        /// </summary>
        public Resolver() : this(GetDnsServers())
        {
        }

        /// <summary>
        /// Constructor of Resolver using DNS servers specified.
        /// </summary>
        /// <param name="dnsServers">Set of DNS servers</param>
        public Resolver(IPEndPoint[] dnsServers)
		{
			this.dnsServers = new List<IPEndPoint>(dnsServers);

			unique = (ushort)(new Random()).Next();
			retries = 3;
			TimeOut = TimeSpan.FromSeconds(3);
			Recursion = true;
			useCache = true;
			TransportType = TransportType.Udp;
		}

		/// <summary>
		/// Constructor of Resolver using DNS server specified.
		/// </summary>
		/// <param name="dnsServer">DNS server to use</param>
		public Resolver(IPEndPoint dnsServer) : this(new [] { dnsServer })
		{
		}

		/// <summary>
		/// Constructor of Resolver using DNS server and port specified.
		/// </summary>
		/// <param name="serverIpAddress">DNS server to use</param>
		/// <param name="serverPortNumber">DNS port to use</param>
		public Resolver(IPAddress serverIpAddress, int serverPortNumber) : this(new IPEndPoint(serverIpAddress,serverPortNumber))
		{
		}

		/// <summary>
		/// Constructor of Resolver using DNS address and port specified.
		/// </summary>
		/// <param name="serverIpAddress">DNS server address to use</param>
		/// <param name="serverPortNumber">DNS port to use</param>
		public Resolver(string serverIpAddress, int serverPortNumber = DefaultPort) : this(IPAddress.Parse(serverIpAddress), serverPortNumber)
		{
		}
		
        public class VerboseOutputEventArgs : EventArgs
		{
			public string Message;
			public VerboseOutputEventArgs(string message)
			{
				Message = message;
			}
		}

		private void FireVerbose(string format, params object[] args)
		{
		    OnVerbose?.Invoke(this, new VerboseEventArgs(string.Format(format, args)));
		}

	

	    public async Task<bool> SetDnsServer(string dnsServer)
	    {
            dnsServers.Clear();

            IPAddress ip;
            if (IPAddress.TryParse(dnsServer, out ip))
                dnsServers.Add(new IPEndPoint(ip, DefaultPort));

            var response = await Query(dnsServer, QType.A);
            if (response.RecordsA.Length > 0)
                dnsServers.Add(new IPEndPoint(response.RecordsA[0].Address, DefaultPort));

	        return dnsServers.Count != 0;
	    }




		/// <summary>
		/// Clear the resolver cache
		/// </summary>
		public void ClearCache()
		{
		    lock (responseCache)
		    {
		        responseCache.Clear();
		    }
		}

		private Response SearchInCache(Question question)
		{
			if (!useCache)
				return null;

			var strKey = question.QClass + "-" + question.QType + "-" + question.QName;

			Response response = null;

			lock (responseCache)
			{
				if (!responseCache.ContainsKey(strKey))
					return null;

				response = responseCache[strKey];
			}

			var timeLived = (int)((DateTime.Now.Ticks - response.TimeStamp.Ticks) / TimeSpan.TicksPerSecond);
			foreach (var rr in response.RecordsRR)
			{
				rr.TimeLived = timeLived;
				// The TTL property calculates its actual time to live
				if (rr.TTL == 0)
					return null; // out of date
			}
			return response;
		}

		private void AddToCache(Response response)
		{
			if (!useCache)
				return;

			// No question, no caching
			if (response.Questions.Count == 0)
				return;

			// Only cached non-error responses
			if (response.header.RCODE != RCode.NoError)
				return;

			var question = response.Questions[0];

			var strKey = question.QClass + "-" + question.QType + "-" + question.QName;

			lock (responseCache)
			{
				if (responseCache.ContainsKey(strKey))
					responseCache.Remove(strKey);

				responseCache.Add(strKey, response);
			}
		}

		private Response UdpRequest(Request request)
		{
			// RFC1035 max. size of a UDP datagram is 512 bytes
			var responseMessage = new byte[4096];

			for (var intAttempts = 0; intAttempts < retries; intAttempts++)
			{
				for (var intDnsServer = 0; intDnsServer < dnsServers.Count; intDnsServer++)
				{
				    using (var socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp))
				    {
				        socket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReceiveTimeout, TimeOut.TotalMilliseconds);

				        try
				        {
				            socket.SendTo(request.Data, dnsServers[intDnsServer]);
				            var intReceived = socket.Receive(responseMessage);
				            var data = new byte[intReceived];
				            Array.Copy(responseMessage, data, intReceived);
				            var response = new Response(dnsServers[intDnsServer], data);
				            AddToCache(response);
				            return response;
				        }
				        catch (SocketException)
				        {
				            FireVerbose(string.Format(";; Connection to nameserver {0} failed", (intDnsServer + 1)));
				        }
				        finally
				        {
				            unique++;
				        }
				    }
				}
			}

		    return new Response { Error = "Timeout Error" };
		}

		private async Task<Response> TcpRequest(Request request)
		{
			//System.Diagnostics.Stopwatch sw = new System.Diagnostics.Stopwatch();
			//sw.Start();

			for (var attempts = 0; attempts < retries; attempts++)
			{
				for (var intDnsServer = 0; intDnsServer < dnsServers.Count; intDnsServer++)
				{
				    using (var tcpClient = new TcpClient {ReceiveTimeout = (int)TimeOut.TotalMilliseconds, SendTimeout = (int)TimeOut.TotalMilliseconds })
				    {
                        //tcpClient.ReceiveBufferSize = ... 8Kb by default
                        //tcpClient.SendBufferSize = ... 8Kb by default

                        try
                        {
                            await tcpClient.ConnectAsync(dnsServers[intDnsServer].Address, dnsServers[intDnsServer].Port);

                            if (!tcpClient.Connected)
                            {
                                FireVerbose(string.Format(";; Connection to nameserver {0} failed", (intDnsServer + 1)));
                                continue;
                            }

                            var bs = tcpClient.GetStream();

                            var data = request.Data;
                            bs.WriteByte((byte)((data.Length >> 8) & 0xff));
                            bs.WriteByte((byte)(data.Length & 0xff));
                            bs.Write(data, 0, data.Length);
                            bs.Flush();

                            var transferResponse = new Response();
                            var intSoa = 0;
                            var intMessageSize = 0;

                            //Debug.WriteLine("Sending "+ (request.Length+2) + " bytes in "+ sw.ElapsedMilliseconds+" mS");

                            while (true)
                            {
                                var intLength = bs.ReadByte() << 8 | bs.ReadByte();
                                if (intLength <= 0)
                                {
                                    FireVerbose(string.Format(";; Connection to nameserver {0} failed", (intDnsServer + 1)));
                                    throw new SocketException(); // next try
                                }

                                intMessageSize += intLength;

                                data = new byte[intLength];
                                bs.Read(data, 0, intLength);
                                var response = new Response(dnsServers[intDnsServer], data);

                                //Debug.WriteLine("Received "+ (intLength+2)+" bytes in "+sw.ElapsedMilliseconds +" mS");

                                if (response.header.RCODE != RCode.NoError)
                                    return response;

                                if (response.Questions[0].QType != QType.AXFR)
                                {
                                    AddToCache(response);
                                    return response;
                                }

                                // Zone transfer!!

                                if (transferResponse.Questions.Count == 0)
                                    transferResponse.Questions.AddRange(response.Questions);
                                transferResponse.Answers.AddRange(response.Answers);
                                transferResponse.Authorities.AddRange(response.Authorities);
                                transferResponse.Additionals.AddRange(response.Additionals);

                                if (response.Answers[0].Type == DnsEntryType.SOA)
                                    intSoa++;

                                if (intSoa == 2)
                                {
                                    transferResponse.header.QDCOUNT = (ushort)transferResponse.Questions.Count;
                                    transferResponse.header.ANCOUNT = (ushort)transferResponse.Answers.Count;
                                    transferResponse.header.NSCOUNT = (ushort)transferResponse.Authorities.Count;
                                    transferResponse.header.ARCOUNT = (ushort)transferResponse.Additionals.Count;
                                    transferResponse.MessageSize = intMessageSize;
                                    return transferResponse;
                                }
                            }
                        }
                        catch (Exception e) when (e is SocketException || e is TimeoutException)
                        {
                            return new Response { Error = e.Message };
                        }
                        finally
                        {
                            unique++;
                        }
                    }
                }
            }
            return new Response { Error = "Timeout Error" };
		}

		/// <summary>
		/// Do Query on specified DNS servers
		/// </summary>
		/// <param name="name">Name to query</param>
		/// <param name="qtype">Question type</param>
		/// <param name="qclass">Class type</param>
		/// <returns>Response of the query</returns>
		public async Task<Response> Query(string name, QType qtype, QClass qclass = QClass.IN)
		{
			Question question = new Question(name, qtype, qclass);
			Response response = SearchInCache(question);
			if (response != null)
				return response;

			Request request = new Request();
			request.AddQuestion(question);
			return await GetResponse(request);
		}


		private async Task<Response> GetResponse(Request request)
		{
			request.header.ID = unique;
			request.header.RD = Recursion;

			if (TransportType == TransportType.Udp)
				return UdpRequest(request);

			if (TransportType == TransportType.Tcp)
				return await TcpRequest(request);

		    return new Response { Error = "Unknown TransportType" };
		}

        /// <summary>
        /// Gets a list of default DNS servers used on the Windows machine.
        /// </summary>
        /// <returns></returns>
        public static IPEndPoint[] GetDnsServers()
        {
            return (from adapter in NetworkInterface.GetAllNetworkInterfaces()
                where adapter.OperationalStatus == OperationalStatus.Up
                let ipProps = adapter.GetIPProperties()
                from ipAddr in ipProps.DnsAddresses
                where ipAddr.AddressFamily == AddressFamily.InterNetwork
                      || (ipAddr.AddressFamily == AddressFamily.InterNetworkV6 && (ipAddr.IsIPv4MappedToIPv6 || ipAddr.IsIPv6Multicast || ipAddr.IsIPv6Teredo))
                select new IPEndPoint(ipAddr, DefaultPort)
            ).ToArray();
        }


        private async Task<IPHostEntry> MakeEntry(string hostName)
		{
		    var entry = new IPHostEntry {HostName = hostName};
		    var response = await Query(hostName, QType.A);

			// fill AddressList and aliases
			var addressList = new List<IPAddress>();
			var aliases = new List<string>();
			foreach (var answerRr in response.Answers)
			{
				if (answerRr.Type == DnsEntryType.A)
				{
					// answerRR.RECORD.ToString() == (answerRR.RECORD as RecordA).Address
					addressList.Add(IPAddress.Parse((answerRr.RECORD.ToString())));
					entry.HostName = answerRr.NAME;
				}
				else
				{
					if (answerRr.Type == DnsEntryType.CNAME)
						aliases.Add(answerRr.NAME);
				}
			}
			entry.AddressList = addressList.ToArray();
			entry.Aliases = aliases.ToArray();

			return entry;
		}

		/// <summary>
		/// Translates the IPV4 or IPV6 address into an arpa address
		/// </summary>
		/// <param name="ip">IP address to get the arpa address form</param>
		/// <returns>The 'mirrored' IPV4 or IPV6 arpa address</returns>
		public static string GetArpaFromIp(IPAddress ip)
		{
			if (ip.AddressFamily == AddressFamily.InterNetwork)
			{
				StringBuilder sb = new StringBuilder();
				sb.Append("in-addr.arpa.");
				foreach (byte b in ip.GetAddressBytes())
				{
					sb.Insert(0, string.Format("{0}.", b));
				}
				return sb.ToString();
			}
			if (ip.AddressFamily == AddressFamily.InterNetworkV6)
			{
				StringBuilder sb = new StringBuilder();
				sb.Append("ip6.arpa.");
				foreach (byte b in ip.GetAddressBytes())
				{
					sb.Insert(0, string.Format("{0:x}.", (b >> 4) & 0xf));
					sb.Insert(0, string.Format("{0:x}.", (b >> 0) & 0xf));
				}
				return sb.ToString();
			}
			return "?";
		}

		public static string GetArpaFromEnum(string strEnum)
		{
			var number = System.Text.RegularExpressions.Regex.Replace(strEnum, "[^0-9]", "");
			var sb = new StringBuilder("e164.arpa.");
			foreach (char c in number)
				sb.Insert(0, string.Format("{0}.", c));
			return sb.ToString();
		}

		/// <summary>
		///		Resolves an IP address to an System.Net.IPHostEntry instance.
		/// </summary>
		/// <param name="ip">An IP address.</param>
		/// <returns>
		///		An System.Net.IPHostEntry instance that contains address information about
		///		the host specified in address.
		///</returns>
		public async Task<IPHostEntry> GetHostEntry(IPAddress ip)
		{
			var response = await Query(GetArpaFromIp(ip), QType.PTR);
			if (response.RecordsPTR.Length > 0)
				return await MakeEntry(response.RecordsPTR[0].PTRDNAME);
            return new IPHostEntry();
		}

		/// <summary>
		///		Resolves a host name or IP address to an System.Net.IPHostEntry instance.
		/// </summary>
		/// <param name="hostNameOrAddress">The host name or IP address to resolve.</param>
		/// <returns>
		///		An System.Net.IPHostEntry instance that contains address information about
		///		the host specified in hostNameOrAddress. 
		///</returns>
		public async Task<IPHostEntry> GetHostEntry(string hostNameOrAddress)
		{
			IPAddress iPAddress;
			if (IPAddress.TryParse(hostNameOrAddress, out iPAddress))
				return await GetHostEntry(iPAddress);
            return await MakeEntry(hostNameOrAddress);
		}
	}
}
