/*
 *  BenchmarkNet is a console application for testing the reliable UDP networking libraries
 *  Copyright (c) 2018 Stanislav Denisov
 *
 *  Permission is hereby granted, free of charge, to any person obtaining a copy
 *  of this software and associated documentation files (the "Software"), to deal
 *  in the Software without restriction, including without limitation the rights
 *  to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
 *  copies of the Software, and to permit persons to whom the Software is
 *  furnished to do so, subject to the following conditions:
 *
 *  The above copyright notice and this permission notice shall be included in all
 *  copies or substantial portions of the Software.
 *
 *  THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
 *  IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
 *  FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
 *  AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
 *  LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
 *  OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
 *  SOFTWARE.
*/

using System;
using System.Diagnostics;
using System.IO;
using System.Runtime;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
// ENet (https://github.com/lsalzman/enet) C# Wrapper (https://github.com/NateShoffner/ENetSharp)
using ENet;
// UNet (https://docs.unity3d.com/Manual/UNetUsingTransport.html)
using UnetServerDll;
// LiteNetLib (https://github.com/RevenantX/LiteNetLib)
using LiteNetLib;
using LiteNetLib.Utils;
// Lidgren (https://github.com/lidgren/lidgren-network-gen3)
using Lidgren.Network;
// MiniUDP (https://github.com/ashoulson/MiniUDP)
using MiniUDP;
// Hazel (https://github.com/DarkRiftNetworking/Hazel-Networking)
using Hazel;
using Hazel.Udp;
// Photon (https://www.photonengine.com/en/OnPremise)
using ExitGames.Client.Photon;

namespace BenchmarkNet {
	public class BenchmarkNet {
		protected const string title = "BenchmarkNet";
		protected const string version = "1.04";
		protected const string ip = "127.0.0.1";
		protected static ushort port = 0;
		protected static ushort maxClients = 0;
		protected static int serverTickRate = 0;
		protected static int clientTickRate = 0;
		protected static int sendRate = 0;
		protected static int reliableMessages = 0;
		protected static int unreliableMessages = 0;
		protected static string message = "";
		protected static char[] reversedMessage;
		protected static byte[] messageData;
		protected static byte[] reversedData;
		protected static bool processActive = false;
		protected static bool processCompleted = false;
		protected static bool processOverload = false;
		protected static bool processFailure = false;
		protected static bool lowLatencyMode = false;
		protected static bool maxClientsPass = true;
		protected static Thread serverThread;
		protected static volatile int clientsStartedCount = 0;
		protected static volatile int clientsConnectedCount = 0;
		protected static volatile int clientsChannelsCount = 0;
		protected static volatile int clientsDisconnectedCount = 0;
		protected static volatile int serverReliableSent = 0;
		protected static volatile int serverReliableReceived = 0;
		protected static volatile int serverReliableBytesSent = 0;
		protected static volatile int serverReliableBytesReceived = 0;
		protected static volatile int serverUnreliableSent = 0;
		protected static volatile int serverUnreliableReceived = 0;
		protected static volatile int serverUnreliableBytesSent = 0;
		protected static volatile int serverUnreliableBytesReceived = 0;
		protected static volatile int clientsReliableSent = 0;
		protected static volatile int clientsReliableReceived = 0;
		protected static volatile int clientsReliableBytesSent = 0;
		protected static volatile int clientsReliableBytesReceived = 0;
		protected static volatile int clientsUnreliableSent = 0;
		protected static volatile int clientsUnreliableReceived = 0;
		protected static volatile int clientsUnreliableBytesSent = 0;
		protected static volatile int clientsUnreliableBytesReceived = 0;
		private static ushort maxPeers = 0;
		private static byte selectedLibrary = 0;
		private static readonly string[] networkingLibraries = {
			"ENet",
			"UNet",
			"LiteNetLib",
			"Lidgren",
			"MiniUDP",
			"Hazel",
			"Photon"
		};

		private static Func<int, string> Space = (value) => ("".PadRight(value));
		private static Func<int, decimal, decimal, decimal> PayloadFlow = (clientsChannelsCount, messageLength, sendRate) => (clientsChannelsCount * (messageLength * sendRate * 2) * 8 / (1000 * 1000)) * 2;
		
		private static void Main(string[] arguments) {
			Console.Title = title;

			for (int i = 0; i < arguments.Length; i++) {
				string argument = arguments[i];

				if (argument == "-lowlatency")
					lowLatencyMode = true;
			}

			Console.SetIn(new StreamReader(Console.OpenStandardInput(8192), Console.InputEncoding, false, bufferSize: 1024));
			Console.WriteLine("Welcome to " + title + "!");
			Console.WriteLine("Version " + version);
			Console.WriteLine(Environment.NewLine + "Source code is available on GitHub (https://github.com/nxrighthere/BenchmarkNet)");
			Console.WriteLine("If you have any questions, contact me (nxrighthere@gmail.com)");
			
			if (lowLatencyMode) {
				Console.ForegroundColor = ConsoleColor.Yellow;
				Console.WriteLine(Environment.NewLine + "The process will perform in Sustained Low Latency mode.");
				Console.ResetColor();
			}
			
			Console.WriteLine(Environment.NewLine + "Select a networking library");

			for (int i = 0; i < networkingLibraries.Length; i++) {
				Console.WriteLine("(" + i + ") " + networkingLibraries[i]);
			}

			Console.Write(Environment.NewLine + "Enter the number (default 0): ");
			Byte.TryParse(Console.ReadLine(), out selectedLibrary);

			ushort defaultPort = 9500;

			Console.Write("Port (default " + defaultPort + "): ");
			UInt16.TryParse(Console.ReadLine(), out port);
			
			if (port == 0)
				port = defaultPort;

			ushort defaultMaxClients = 1000;

			Console.Write("Simulated clients (default " + defaultMaxClients + "): ");
			UInt16.TryParse(Console.ReadLine(), out maxClients);

			if (maxClients == 0)
				maxClients = defaultMaxClients;

			int defaultServerTickRate = 64;

			Console.Write("Server tick rate (default " + defaultServerTickRate + "): ");
			Int32.TryParse(Console.ReadLine(), out serverTickRate);

			if (serverTickRate == 0)
				serverTickRate = defaultServerTickRate;

			int defaultClientTickRate = 64;

			Console.Write("Client tick rate (default " + defaultClientTickRate + "): ");
			Int32.TryParse(Console.ReadLine(), out clientTickRate);

			if (clientTickRate == 0)
				clientTickRate = defaultClientTickRate;

			int defaultSendRate = 15;

			Console.Write("Client send rate (default " + defaultSendRate + "): ");
			Int32.TryParse(Console.ReadLine(), out sendRate);

			if (sendRate == 0)
				sendRate = defaultSendRate;

			int defaultReliableMessages = 500;

			Console.Write("Reliable messages per client (default " + defaultReliableMessages + "): ");
			Int32.TryParse(Console.ReadLine(), out reliableMessages);

			if (reliableMessages == 0)
				reliableMessages = defaultReliableMessages;

			int defaultUnreliableMessages = 1000;

			Console.Write("Unreliable messages per client (default " + defaultUnreliableMessages + "): ");
			Int32.TryParse(Console.ReadLine(), out unreliableMessages);

			if (unreliableMessages == 0)
				unreliableMessages = defaultUnreliableMessages;

			string defaultMessage = "Sometimes we just need a good networking library";

			Console.Write("Message (default " + defaultMessage.Length + " characters): ");
			message = Console.ReadLine();

			if (message == string.Empty)
				message = defaultMessage;

			reversedMessage = message.ToCharArray();
			Array.Reverse(reversedMessage);
			messageData = Encoding.ASCII.GetBytes(message);
			reversedData = Encoding.ASCII.GetBytes(new string(reversedMessage));

			Console.CursorVisible = false;
			Console.Clear();
			
			processActive = true;

			if (lowLatencyMode)
				GCSettings.LatencyMode = GCLatencyMode.SustainedLowLatency;

			if (selectedLibrary == 0)
				Library.Initialize();

			maxPeers = ushort.MaxValue;
			maxClientsPass = (selectedLibrary > 0 ? maxClients <= maxPeers : maxClients <= ENet.Native.ENET_PROTOCOL_MAXIMUM_PEER_ID);

			if (!maxClientsPass)
				maxClients = Math.Min(Math.Max((ushort)1, (ushort)maxClients), (selectedLibrary > 0 ? maxPeers : (ushort)Native.ENET_PROTOCOL_MAXIMUM_PEER_ID));

			if (selectedLibrary == 0)
				serverThread = new Thread(ENetBenchmark.Server);
			else if (selectedLibrary == 1)
				serverThread = new Thread(UNetBenchmark.Server);
			else if (selectedLibrary == 2)
				serverThread = new Thread(LiteNetLibBenchmark.Server);
			else if (selectedLibrary == 3)
				serverThread = new Thread(LidgrenBenchmark.Server);
			else if (selectedLibrary == 4)
				serverThread = new Thread(MiniUDPBenchmark.Server);
			else if (selectedLibrary == 5)
				serverThread = new Thread(HazelBenchmark.Server);
			else
				serverThread = new Thread(PhotonBenchmark.Server);

			serverThread.Start();
			Thread.Sleep(100);
			
			Task infoTask = Info();
			Task superviseTask = Supervise();
			Task spawnTask = Spawn();

			Console.ReadKey();
			processActive = false;
			Environment.Exit(0);
		}

		private static async Task Info() {
			await Task.Factory.StartNew(() => {
				int spinnerTimer = 0;
				int spinnerSequence = 0;
				string spinner = "";
				Stopwatch elapsedTime = new Stopwatch();

				elapsedTime.Start();

				while (processActive) {
					Console.CursorVisible = false;
					Console.SetCursorPosition(0, 0);
					Console.WriteLine("Benchmarking " + networkingLibraries[selectedLibrary] + "...");
					Console.WriteLine("Server tick rate: " + serverTickRate + ", Client tick rate: " + clientTickRate + " (ticks per second)");
					Console.WriteLine(maxClients + " clients, " + reliableMessages + " reliable and " + unreliableMessages + " unreliable messages per client, " + messageData.Length + " bytes per message, " + sendRate + " messages per second");
					
					if (!maxClientsPass) {
						Console.ForegroundColor = ConsoleColor.Red;
						Console.WriteLine("This networking library doesn't support more than " + (selectedLibrary > 0 ? maxPeers : Native.ENET_PROTOCOL_MAXIMUM_PEER_ID) + " peers per server!");
						Console.ResetColor();
					}

					if (lowLatencyMode) {
						Console.ForegroundColor = ConsoleColor.Yellow;
						Console.WriteLine(Environment.NewLine + "The process is performing in Sustained Low Latency mode.");
						Console.ResetColor();
					}

					Console.WriteLine(Environment.NewLine + "Server status: " + (processFailure || !serverThread.IsAlive ? "Failure" + Space(2) : (processOverload ? "Overload" + Space(1) : (processCompleted ? "Completed" : "Running" + Space(2)))));
					Console.WriteLine("Clients status: " + clientsStartedCount + " started, " + clientsConnectedCount + " connected, " + clientsDisconnectedCount + " dropped");
					Console.WriteLine("Server payload flow: " + PayloadFlow(clientsChannelsCount, messageData.Length, sendRate).ToString("0.00") + " mbps (current), " + PayloadFlow(maxClients * 2, messageData.Length, sendRate).ToString("0.00") + " mbps (predicted)" + Space(10));
					Console.WriteLine("Clients sent -> Reliable: " + clientsReliableSent + " messages (" + clientsReliableBytesSent + " bytes), Unreliable: " + clientsUnreliableSent + " messages (" + clientsUnreliableBytesSent + " bytes)");
					Console.WriteLine("Server received <- Reliable: " + serverReliableReceived + " messages (" + serverReliableBytesReceived + " bytes), Unreliable: " + serverUnreliableReceived + " messages (" + serverUnreliableBytesReceived + " bytes)");
					Console.WriteLine("Server sent -> Reliable: " + serverReliableSent + " messages (" + serverReliableBytesSent + " bytes), Unreliable: " + serverUnreliableSent + " messages (" + serverUnreliableBytesSent + " bytes)");
					Console.WriteLine("Clients received <- Reliable: " + clientsReliableReceived + " messages (" + clientsReliableBytesReceived + " bytes), Unreliable: " + clientsUnreliableReceived + " messages (" + clientsUnreliableBytesReceived + " bytes)");
					Console.WriteLine("Total - Reliable: " + ((ulong)clientsReliableSent + (ulong)serverReliableReceived + (ulong)serverReliableSent + (ulong)clientsReliableReceived) + " messages (" + ((ulong)clientsReliableBytesSent + (ulong)serverReliableBytesReceived + (ulong)serverReliableBytesSent + (ulong)clientsReliableBytesReceived) + " bytes), Unreliable: " + ((ulong)clientsUnreliableSent + (ulong)serverUnreliableReceived + (ulong)serverUnreliableSent + (ulong)clientsUnreliableReceived) + " messages (" + ((ulong)clientsUnreliableBytesSent + (ulong)serverUnreliableBytesReceived + (ulong)serverUnreliableBytesSent + (ulong)clientsUnreliableBytesReceived) + " bytes)");
					Console.WriteLine("Expected - Reliable: " + (maxClients * (ulong)reliableMessages * 4) + " messages (" + (maxClients * (ulong)reliableMessages * (ulong)messageData.Length * 4) + " bytes), Unreliable: " + (maxClients * (ulong)unreliableMessages * 4) + " messages (" + (maxClients * (ulong)unreliableMessages * (ulong)messageData.Length * 4) + " bytes)");
					Console.WriteLine("Elapsed time: " + elapsedTime.Elapsed.Hours.ToString("00") + ":" + elapsedTime.Elapsed.Minutes.ToString("00") + ":" + elapsedTime.Elapsed.Seconds.ToString("00"));

					if (spinnerTimer >= 10) {
						spinnerSequence++;
						spinnerTimer = 0;
					} else {
						spinnerTimer++;
					}

					switch (spinnerSequence % 4) {
						case 0: spinner = "/";
							break;
						case 1: spinner = "—";
							break;
						case 2: spinner = "\\";
							break;
						case 3: spinner = "|";
							break;
					}

					Console.WriteLine(Environment.NewLine + "Press any key to stop the process " + spinner);
					Thread.Sleep(15);
				}

				if (!processActive && processCompleted) {
					Console.SetCursorPosition(0, Console.CursorTop - 1);
					Console.WriteLine("Process completed! Press any key to exit...");
				}
				
				elapsedTime.Stop();
			}, TaskCreationOptions.LongRunning);
		}

		private static async Task Supervise() {
			await Task.Factory.StartNew(() => {
				decimal lastData = 0;

				while (processActive) {
					Thread.Sleep(1000);

					decimal currentData = ((decimal)serverReliableSent + (decimal)serverReliableReceived + (decimal)serverUnreliableSent + (decimal)serverUnreliableReceived + (decimal)clientsReliableSent + (decimal)clientsReliableReceived + (decimal)clientsUnreliableSent + (decimal)clientsUnreliableReceived);

					if (currentData == lastData) {
						if (currentData != 0 && ((currentData / (maxClients * ((decimal)reliableMessages + (decimal)unreliableMessages) * 4)) * 100) < 90)
							processOverload = true;

						processCompleted = true;
						Thread.Sleep(100);
						processActive = false;

						break;
					}
					
					lastData = currentData;
				}

				if (selectedLibrary == 0)
					Library.Deinitialize();
			}, TaskCreationOptions.LongRunning);
		}

		private static async Task Spawn() {
			await Task.Factory.StartNew(() => {
				Task[] clients = new Task[maxClients];

				for (int i = 0; i < maxClients; i++) {
					if (!processActive)
						break;

					if (selectedLibrary == 0)
						clients[i] = ENetBenchmark.Client();
					else if (selectedLibrary == 1)
						clients[i] = UNetBenchmark.Client();
					else if (selectedLibrary == 2)
						clients[i] = LiteNetLibBenchmark.Client();
					else if (selectedLibrary == 3)
						clients[i] = LidgrenBenchmark.Client();
					else if (selectedLibrary == 4)
						clients[i] = MiniUDPBenchmark.Client();
					else if (selectedLibrary == 5)
						clients[i] = HazelBenchmark.Client();
					else
						clients[i] = PhotonBenchmark.Client();
					
					Interlocked.Increment(ref clientsStartedCount);
					Thread.Sleep(15);
				}
			}, TaskCreationOptions.LongRunning);
		}
	}

	public class ENetBenchmark : BenchmarkNet {
		private static void SendReliable(byte[] data, byte channelID, Peer peer) {
			Packet packet = new Packet();
			packet.Create(data, 0, data.Length, PacketFlags.Reliable); // Reliable Sequenced
			peer.Send(channelID, packet);
		}

		private static void SendUnreliable(byte[] data, byte channelID, Peer peer) {
			Packet packet = new Packet();
			packet.Create(data, 0, data.Length, PacketFlags.None); // Unreliable Sequenced
			peer.Send(channelID, packet);
		}

		public static void Server() {
			using (Host server = new Host()) {
				server.Create(port, maxClients);
				
				Event netEvent = new Event();

				while (processActive) {
					server.Service(1000 / serverTickRate, out netEvent);

					switch (netEvent.Type) {
						case EventType.Receive:
							byte[] data = netEvent.Packet.GetBytes();

							if (netEvent.ChannelID == 2) {
								Interlocked.Increment(ref serverReliableReceived);
								Interlocked.Add(ref serverReliableBytesReceived, data.Length);
								SendReliable(messageData, 0, netEvent.Peer);
								Interlocked.Increment(ref serverReliableSent);
								Interlocked.Add(ref serverReliableBytesSent, messageData.Length);
							} else if (netEvent.ChannelID == 3) {
								Interlocked.Increment(ref serverUnreliableReceived);
								Interlocked.Add(ref serverUnreliableBytesReceived, data.Length);
								SendUnreliable(messageData, 1, netEvent.Peer);
								Interlocked.Increment(ref serverUnreliableSent);
								Interlocked.Add(ref serverUnreliableBytesSent, messageData.Length);
							}

							netEvent.Packet.Dispose();

							break;
					}
				}
			}
		}

		public static async Task Client() {
			await Task.Factory.StartNew(() => {
				Host client = new Host();
				
				client.Create(null, 1);

				Address address = new Address();

				address.SetHost(ip);
				address.Port = port;

				Peer peer = client.Connect(address, 4, 0);
				Event netEvent = new Event();
				
				int reliableToSend = 0;
				int unreliableToSend = 0;
				int reliableSentCount = 0;
				int unreliableSentCount = 0;

				Task.Factory.StartNew(async() => {
					bool reliableIncremented = false;
					bool unreliableIncremented = false;

					while (processActive) {
						if (reliableToSend > 0) {
							SendReliable(messageData, 2, peer);
							Interlocked.Decrement(ref reliableToSend);
							Interlocked.Increment(ref reliableSentCount);
							Interlocked.Increment(ref clientsReliableSent);
							Interlocked.Add(ref clientsReliableBytesSent, messageData.Length);
						}

						if (unreliableToSend > 0) {
							SendUnreliable(messageData, 3, peer);
							Interlocked.Decrement(ref unreliableToSend);
							Interlocked.Increment(ref unreliableSentCount);
							Interlocked.Increment(ref clientsUnreliableSent);
							Interlocked.Add(ref clientsUnreliableBytesSent, messageData.Length);
						}

						if (reliableToSend > 0 && !reliableIncremented) {
							reliableIncremented = true;
							Interlocked.Increment(ref clientsChannelsCount);
						} else if (reliableToSend == 0 && reliableIncremented) {
							reliableIncremented = false;
							Interlocked.Decrement(ref clientsChannelsCount);
						}

						if (unreliableToSend > 0 && !unreliableIncremented) {
							unreliableIncremented = true;
							Interlocked.Increment(ref clientsChannelsCount);
						} else if (unreliableToSend == 0 && unreliableIncremented) {
							unreliableIncremented = false;
							Interlocked.Decrement(ref clientsChannelsCount);
						}

						await Task.Delay(1000 / sendRate);
					}
				}, TaskCreationOptions.AttachedToParent);

				while (processActive) {
					client.Service(1000 / clientTickRate, out netEvent);

					switch (netEvent.Type) {
						case EventType.Connect:
							Interlocked.Increment(ref clientsConnectedCount);
							Interlocked.Exchange(ref reliableToSend, reliableMessages);
							Interlocked.Exchange(ref unreliableToSend, unreliableMessages);

							break;

						case EventType.Disconnect:
							Interlocked.Increment(ref clientsDisconnectedCount);
							Interlocked.Exchange(ref reliableToSend, 0);
							Interlocked.Exchange(ref unreliableToSend, 0);

							break;

						case EventType.Receive:
							byte[] data = netEvent.Packet.GetBytes();

							if (netEvent.ChannelID == 0) {
								Interlocked.Increment(ref clientsReliableReceived);
								Interlocked.Add(ref clientsReliableBytesReceived, data.Length);
							} else if (netEvent.ChannelID == 1) {
								Interlocked.Increment(ref clientsUnreliableReceived);
								Interlocked.Add(ref clientsUnreliableBytesReceived, data.Length);
							}
							
							netEvent.Packet.Dispose();

							break;
					}
				}

				peer.Disconnect(0);
			}, TaskCreationOptions.LongRunning);
		}
	}

	public class UNetBenchmark : BenchmarkNet {
		public static void Server() {
			GlobalConfig globalConfig = new GlobalConfig();

			globalConfig.ReactorMaximumSentMessages = 0;
			globalConfig.ReactorMaximumReceivedMessages = 0;

			ConnectionConfig connectionConfig = new ConnectionConfig();

			int reliableChannel = connectionConfig.AddChannel(QosType.ReliableSequenced);
			int unreliableChannel = connectionConfig.AddChannel(QosType.UnreliableSequenced);

			HostTopology topology = new HostTopology(connectionConfig, maxClients);
			
			topology.SentMessagePoolSize = ushort.MaxValue;
			topology.ReceivedMessagePoolSize = ushort.MaxValue;

			using (NetLibraryManager server = new NetLibraryManager(globalConfig)) {
				int host = server.AddHost(topology, port, ip);

				int hostID, connectionID, channelID, dataLength;
				byte[] buffer = new byte[1024];
				NetworkEventType netEvent;

				while (processActive) {
					byte error;

					while ((netEvent = server.Receive(out hostID, out connectionID, out channelID, buffer, buffer.Length, out dataLength, out error)) != NetworkEventType.Nothing) {
						switch (netEvent) {
							case NetworkEventType.DataEvent:
								if (channelID == 0) {
									Interlocked.Increment(ref serverReliableReceived);
									Interlocked.Add(ref serverReliableBytesReceived, dataLength);
									server.Send(hostID, connectionID, reliableChannel, messageData, messageData.Length, out error); 
									Interlocked.Increment(ref serverReliableSent);
									Interlocked.Add(ref serverReliableBytesSent, messageData.Length);
								} else if (channelID == 1) {
									Interlocked.Increment(ref serverUnreliableReceived);
									Interlocked.Add(ref serverUnreliableBytesReceived, dataLength);
									server.Send(hostID, connectionID, unreliableChannel, messageData, messageData.Length, out error); 
									Interlocked.Increment(ref serverUnreliableSent);
									Interlocked.Add(ref serverUnreliableBytesSent, messageData.Length);
								}

								break;
						}
					}

					Thread.Sleep(1000 / serverTickRate);
				}
			}
		}

		public static async Task Client() {
			await Task.Factory.StartNew(() => {
				ConnectionConfig connectionConfig = new ConnectionConfig();
				
				int reliableChannel = connectionConfig.AddChannel(QosType.ReliableSequenced);
				int unreliableChannel = connectionConfig.AddChannel(QosType.UnreliableSequenced);
				
				NetLibraryManager client = new NetLibraryManager();
				int host = client.AddHost(new HostTopology(connectionConfig, 1), 0, null);
				
				byte connectionError;
				int connection = client.Connect(host, ip, port, 0, out connectionError);

				int reliableToSend = 0;
				int unreliableToSend = 0;
				int reliableSentCount = 0;
				int unreliableSentCount = 0;

				Task.Factory.StartNew(async() => {
					bool reliableIncremented = false;
					bool unreliableIncremented = false;

					while (processActive) {
						byte error;
						
						if (reliableToSend > 0) {
							client.Send(host, connection, reliableChannel, messageData, messageData.Length, out error); 
							Interlocked.Decrement(ref reliableToSend);
							Interlocked.Increment(ref reliableSentCount);
							Interlocked.Increment(ref clientsReliableSent);
							Interlocked.Add(ref clientsReliableBytesSent, messageData.Length);
						}

						if (unreliableToSend > 0) {
							client.Send(host, connection, unreliableChannel, messageData, messageData.Length, out error); 
							Interlocked.Decrement(ref unreliableToSend);
							Interlocked.Increment(ref unreliableSentCount);
							Interlocked.Increment(ref clientsUnreliableSent);
							Interlocked.Add(ref clientsUnreliableBytesSent, messageData.Length);
						}

						if (reliableToSend > 0 && !reliableIncremented) {
							reliableIncremented = true;
							Interlocked.Increment(ref clientsChannelsCount);
						} else if (reliableToSend == 0 && reliableIncremented) {
							reliableIncremented = false;
							Interlocked.Decrement(ref clientsChannelsCount);
						}

						if (unreliableToSend > 0 && !unreliableIncremented) {
							unreliableIncremented = true;
							Interlocked.Increment(ref clientsChannelsCount);
						} else if (unreliableToSend == 0 && unreliableIncremented) {
							unreliableIncremented = false;
							Interlocked.Decrement(ref clientsChannelsCount);
						}

						await Task.Delay(1000 / sendRate);
					}
				}, TaskCreationOptions.AttachedToParent);

				int hostID, connectionID, channelID, dataLength;
				byte[] buffer = new byte[1024];
				NetworkEventType netEvent;

				while (processActive) {
					byte error;

					while ((netEvent = client.Receive(out hostID, out connectionID, out channelID, buffer, buffer.Length, out dataLength, out error)) != NetworkEventType.Nothing) {
						switch (netEvent) {
							case NetworkEventType.ConnectEvent:
								Interlocked.Increment(ref clientsConnectedCount);
								Interlocked.Exchange(ref reliableToSend, reliableMessages);
								Interlocked.Exchange(ref unreliableToSend, unreliableMessages);

								break;

							case NetworkEventType.DisconnectEvent:
								Interlocked.Increment(ref clientsDisconnectedCount);
								Interlocked.Exchange(ref reliableToSend, 0);
								Interlocked.Exchange(ref unreliableToSend, 0);

								break;

							case NetworkEventType.DataEvent:
								if (channelID == 0) {
									Interlocked.Increment(ref clientsReliableReceived);
									Interlocked.Add(ref clientsReliableBytesReceived, dataLength);
								} else if (channelID == 1) {
									Interlocked.Increment(ref clientsUnreliableReceived);
									Interlocked.Add(ref clientsUnreliableBytesReceived, dataLength);
								}

								break;
						}
					}

					Thread.Sleep(1000 / clientTickRate);
				}

				byte disconnectError;

				client.Disconnect(host, connection, out disconnectError);
			}, TaskCreationOptions.LongRunning);
		}
	}

	public class LiteNetLibBenchmark : BenchmarkNet {
		private static void SendReliable(byte[] data, LiteNetLib.NetPeer peer) {
			peer.Send(data, DeliveryMethod.ReliableOrdered); // Reliable Ordered (https://github.com/RevenantX/LiteNetLib/issues/68)
		}

		private static void SendUnreliable(byte[] data, LiteNetLib.NetPeer peer) {
			peer.Send(data, DeliveryMethod.Sequenced); // Unreliable Sequenced
		}

		public static void Server() {
			EventBasedNetListener listener = new EventBasedNetListener();
			NetManager server = new NetManager(listener, maxClients);
			
			server.MergeEnabled = true;
			server.Start(port);

			listener.ConnectionRequestEvent += (request) => {
				request.AcceptIfKey(title + "Key");
			};

			listener.NetworkReceiveEvent += (peer, reader, deliveryMethod) => {
				byte[] data = reader.Data;

				if (deliveryMethod == DeliveryMethod.ReliableOrdered) {
					Interlocked.Increment(ref serverReliableReceived);
					Interlocked.Add(ref serverReliableBytesReceived, data.Length);
					SendReliable(messageData, peer);
					Interlocked.Increment(ref serverReliableSent);
					Interlocked.Add(ref serverReliableBytesSent, messageData.Length);
				} else if (deliveryMethod == DeliveryMethod.Sequenced) {
					Interlocked.Increment(ref serverUnreliableReceived);
					Interlocked.Add(ref serverUnreliableBytesReceived, data.Length);
					SendUnreliable(messageData, peer);
					Interlocked.Increment(ref serverUnreliableSent);
					Interlocked.Add(ref serverUnreliableBytesSent, messageData.Length);
				}
			};

			while (processActive) {
				server.PollEvents();
				Thread.Sleep(1000 / serverTickRate);
			}

			server.Stop();
		}

		public static async Task Client() {
			await Task.Factory.StartNew(() => {
				EventBasedNetListener listener = new EventBasedNetListener();
				NetManager client = new NetManager(listener);
				
				client.MergeEnabled = true;
				client.Start();
				client.Connect(ip, port, title + "Key");

				int reliableToSend = 0;
				int unreliableToSend = 0;
				int reliableSentCount = 0;
				int unreliableSentCount = 0;

				Task.Factory.StartNew(async() => {
					bool reliableIncremented = false;
					bool unreliableIncremented = false;

					while (processActive) {
						if (reliableToSend > 0) {
							SendReliable(messageData, client.GetFirstPeer());
							Interlocked.Decrement(ref reliableToSend);
							Interlocked.Increment(ref reliableSentCount);
							Interlocked.Increment(ref clientsReliableSent);
							Interlocked.Add(ref clientsReliableBytesSent, messageData.Length);
						}

						if (unreliableToSend > 0) {
							SendUnreliable(messageData, client.GetFirstPeer());
							Interlocked.Decrement(ref unreliableToSend);
							Interlocked.Increment(ref unreliableSentCount);
							Interlocked.Increment(ref clientsUnreliableSent);
							Interlocked.Add(ref clientsUnreliableBytesSent, messageData.Length);
						}

						if (reliableToSend > 0 && !reliableIncremented) {
							reliableIncremented = true;
							Interlocked.Increment(ref clientsChannelsCount);
						} else if (reliableToSend == 0 && reliableIncremented) {
							reliableIncremented = false;
							Interlocked.Decrement(ref clientsChannelsCount);
						}

						if (unreliableToSend > 0 && !unreliableIncremented) {
							unreliableIncremented = true;
							Interlocked.Increment(ref clientsChannelsCount);
						} else if (unreliableToSend == 0 && unreliableIncremented) {
							unreliableIncremented = false;
							Interlocked.Decrement(ref clientsChannelsCount);
						}

						await Task.Delay(1000 / sendRate);
					}
				}, TaskCreationOptions.AttachedToParent);

				listener.PeerConnectedEvent += (peer) => {
					Interlocked.Increment(ref clientsConnectedCount);
					Interlocked.Exchange(ref reliableToSend, reliableMessages);
					Interlocked.Exchange(ref unreliableToSend, unreliableMessages);
				};

				listener.PeerDisconnectedEvent += (peer, info) => {
					Interlocked.Increment(ref clientsDisconnectedCount);
					Interlocked.Exchange(ref reliableToSend, 0);
					Interlocked.Exchange(ref unreliableToSend, 0);
				};

				listener.NetworkReceiveEvent += (peer, reader, deliveryMethod) => {
					byte[] data = reader.Data;

					if (deliveryMethod == DeliveryMethod.ReliableOrdered) {
						Interlocked.Increment(ref clientsReliableReceived);
						Interlocked.Add(ref clientsReliableBytesReceived, data.Length);
					} else if (deliveryMethod == DeliveryMethod.Sequenced) {
						Interlocked.Increment(ref clientsUnreliableReceived);
						Interlocked.Add(ref clientsUnreliableBytesReceived, data.Length);
					}
				};

				while (processActive) {
					client.PollEvents();
					Thread.Sleep(1000 / clientTickRate);
				}

				client.Stop();
			}, TaskCreationOptions.LongRunning);
		}
	}

	public class LidgrenBenchmark : BenchmarkNet {
		private static void SendReliable(byte[] data, NetConnection connection, NetOutgoingMessage netMessage, int channelID) {
			netMessage.Write(data);
			connection.SendMessage(netMessage, NetDeliveryMethod.ReliableSequenced, channelID);
		}

		private static void SendUnreliable(byte[] data, NetConnection connection, NetOutgoingMessage netMessage, int channelID) {
			netMessage.Write(data);
			connection.SendMessage(netMessage, NetDeliveryMethod.UnreliableSequenced, channelID);
		}

		public static void Server() {
			NetPeerConfiguration config = new NetPeerConfiguration(title + "Config");
			
			config.Port = port;
			config.MaximumConnections = maxClients;

			NetServer server = new NetServer(config);
			server.Start();

			NetIncomingMessage netMessage;

			while (processActive) {
				while ((netMessage = server.ReadMessage()) != null) {
					switch (netMessage.MessageType) {
						case NetIncomingMessageType.Data:
							byte[] data = netMessage.ReadBytes(netMessage.LengthBytes);

							if (netMessage.SequenceChannel == 2) {
								Interlocked.Increment(ref serverReliableReceived);
								Interlocked.Add(ref serverReliableBytesReceived, data.Length);
								SendReliable(messageData, netMessage.SenderConnection, server.CreateMessage(), 0);
								Interlocked.Increment(ref serverReliableSent);
								Interlocked.Add(ref serverReliableBytesSent, messageData.Length);
							} else if (netMessage.SequenceChannel == 3) {
								Interlocked.Increment(ref serverUnreliableReceived);
								Interlocked.Add(ref serverUnreliableBytesReceived, data.Length);
								SendUnreliable(messageData, netMessage.SenderConnection, server.CreateMessage(), 1);
								Interlocked.Increment(ref serverUnreliableSent);
								Interlocked.Add(ref serverUnreliableBytesSent, messageData.Length);
							}

							break;
					}

					server.Recycle(netMessage);
				}
				
				Thread.Sleep(1000 / serverTickRate);
			}

			server.Shutdown(title + "Shutdown");
		}

		public static async Task Client() {
			await Task.Factory.StartNew(() => {
				NetPeerConfiguration config = new NetPeerConfiguration(title + "Config");
				NetClient client = new NetClient(config);
				
				client.Start();
				client.Connect(ip, port);

				int reliableToSend = 0;
				int unreliableToSend = 0;
				int reliableSentCount = 0;
				int unreliableSentCount = 0;

				Task.Factory.StartNew(async() => {
					bool reliableIncremented = false;
					bool unreliableIncremented = false;

					while (processActive) {
						if (reliableToSend > 0) {
							SendReliable(messageData, client.ServerConnection, client.CreateMessage(), 2);
							Interlocked.Decrement(ref reliableToSend);
							Interlocked.Increment(ref reliableSentCount);
							Interlocked.Increment(ref clientsReliableSent);
							Interlocked.Add(ref clientsReliableBytesSent, messageData.Length);
						}

						if (unreliableToSend > 0) {
							SendUnreliable(messageData, client.ServerConnection, client.CreateMessage(), 3);
							Interlocked.Decrement(ref unreliableToSend);
							Interlocked.Increment(ref unreliableSentCount);
							Interlocked.Increment(ref clientsUnreliableSent);
							Interlocked.Add(ref clientsUnreliableBytesSent, messageData.Length);
						}

						if (reliableToSend > 0 && !reliableIncremented) {
							reliableIncremented = true;
							Interlocked.Increment(ref clientsChannelsCount);
						} else if (reliableToSend == 0 && reliableIncremented) {
							reliableIncremented = false;
							Interlocked.Decrement(ref clientsChannelsCount);
						}

						if (unreliableToSend > 0 && !unreliableIncremented) {
							unreliableIncremented = true;
							Interlocked.Increment(ref clientsChannelsCount);
						} else if (unreliableToSend == 0 && unreliableIncremented) {
							unreliableIncremented = false;
							Interlocked.Decrement(ref clientsChannelsCount);
						}

						await Task.Delay(1000 / sendRate);
					}
				}, TaskCreationOptions.AttachedToParent);

				NetIncomingMessage netMessage;

				while (processActive) {
					while ((netMessage = client.ReadMessage()) != null) {
						switch (netMessage.MessageType) {
							case NetIncomingMessageType.StatusChanged:
								NetConnectionStatus status = (NetConnectionStatus)netMessage.ReadByte();

								if (status == NetConnectionStatus.Connected) {
									Interlocked.Increment(ref clientsConnectedCount);
									Interlocked.Exchange(ref reliableToSend, reliableMessages);
									Interlocked.Exchange(ref unreliableToSend, unreliableMessages);
								} else if (status == NetConnectionStatus.Disconnected) {
									Interlocked.Increment(ref clientsDisconnectedCount);
									Interlocked.Exchange(ref reliableToSend, 0);
									Interlocked.Exchange(ref unreliableToSend, 0);
								}

								break;

							case NetIncomingMessageType.Data:
								byte[] data = netMessage.ReadBytes(netMessage.LengthBytes);

								if (netMessage.SequenceChannel == 0) {
									Interlocked.Increment(ref clientsReliableReceived);
									Interlocked.Add(ref clientsReliableBytesReceived, data.Length);
								} else if (netMessage.SequenceChannel == 1) {
									Interlocked.Increment(ref clientsUnreliableReceived);
									Interlocked.Add(ref clientsUnreliableBytesReceived, data.Length);
								}

								break;
						}

						client.Recycle(netMessage);
					}
					
					Thread.Sleep(1000 / clientTickRate);
				}

				client.Shutdown(title + "Shutdown");
			}, TaskCreationOptions.LongRunning);
		}
	}

	public class MiniUDPBenchmark : BenchmarkNet {
		private static void SendReliable(byte[] data, MiniUDP.NetPeer peer) {
			peer.QueueNotification(data, (ushort)data.Length); // Reliable Ordered (https://github.com/ashoulson/MiniUDP/blob/master/MiniUDP/Threaded/NetPeer.cs#L105)
		}

		private static void SendUnreliable(byte[] data, MiniUDP.NetPeer peer) {
			peer.SendPayload(data, (ushort)data.Length); // Unreliable Sequenced
		}

		public static void Server() {
			NetCore server = new NetCore(title, true);

			server.Host(port);
			
			server.PeerNotification += (peer, data, dataLength) => {
				Interlocked.Increment(ref serverReliableReceived);
				Interlocked.Add(ref serverReliableBytesReceived, dataLength);
				SendReliable(messageData, peer);
				Interlocked.Increment(ref serverReliableSent);
				Interlocked.Add(ref serverReliableBytesSent, messageData.Length);
			};

			server.PeerPayload += (peer, data, dataLength) => {
				Interlocked.Increment(ref serverUnreliableReceived);
				Interlocked.Add(ref serverUnreliableBytesReceived, dataLength);
				SendUnreliable(messageData, peer);
				Interlocked.Increment(ref serverUnreliableSent);
				Interlocked.Add(ref serverUnreliableBytesSent, messageData.Length);
			};

			while (processActive) {
				server.PollEvents();
				Thread.Sleep(1000 / serverTickRate);
			}

			server.Stop();
		}

		public static async Task Client() {
			await Task.Factory.StartNew(() => {
				NetCore client = new NetCore(title, false);
				
				MiniUDP.NetPeer connection = client.Connect(NetUtil.StringToEndPoint(ip + ":" + port), "");

				int reliableToSend = 0;
				int unreliableToSend = 0;
				int reliableSentCount = 0;
				int unreliableSentCount = 0;

				Task.Factory.StartNew(async() => {
					bool reliableIncremented = false;
					bool unreliableIncremented = false;

					while (processActive) {
						if (reliableToSend > 0) {
							SendReliable(messageData, connection);
							Interlocked.Decrement(ref reliableToSend);
							Interlocked.Increment(ref reliableSentCount);
							Interlocked.Increment(ref clientsReliableSent);
							Interlocked.Add(ref clientsReliableBytesSent, messageData.Length);
						}

						if (unreliableToSend > 0) {
							SendUnreliable(messageData, connection);
							Interlocked.Decrement(ref unreliableToSend);
							Interlocked.Increment(ref unreliableSentCount);
							Interlocked.Increment(ref clientsUnreliableSent);
							Interlocked.Add(ref clientsUnreliableBytesSent, messageData.Length);
						}

						if (reliableToSend > 0 && !reliableIncremented) {
							reliableIncremented = true;
							Interlocked.Increment(ref clientsChannelsCount);
						} else if (reliableToSend == 0 && reliableIncremented) {
							reliableIncremented = false;
							Interlocked.Decrement(ref clientsChannelsCount);
						}

						if (unreliableToSend > 0 && !unreliableIncremented) {
							unreliableIncremented = true;
							Interlocked.Increment(ref clientsChannelsCount);
						} else if (unreliableToSend == 0 && unreliableIncremented) {
							unreliableIncremented = false;
							Interlocked.Decrement(ref clientsChannelsCount);
						}

						await Task.Delay(1000 / sendRate);
					}
				}, TaskCreationOptions.AttachedToParent);

				client.PeerConnected += (peer, token) => {
					Interlocked.Increment(ref clientsConnectedCount);
					Interlocked.Exchange(ref reliableToSend, reliableMessages);
					Interlocked.Exchange(ref unreliableToSend, unreliableMessages);
				};

				client.PeerClosed += (peer, reason, kickReason, error) => {
					Interlocked.Increment(ref clientsDisconnectedCount);
					Interlocked.Exchange(ref reliableToSend, 0);
					Interlocked.Exchange(ref unreliableToSend, 0);
				};

				client.PeerNotification += (peer, data, dataLength) => {
					Interlocked.Increment(ref clientsReliableReceived);
					Interlocked.Add(ref clientsReliableBytesReceived, dataLength);
				};

				client.PeerPayload += (peer, data, dataLength) => {
					Interlocked.Increment(ref clientsUnreliableReceived);
					Interlocked.Add(ref clientsUnreliableBytesReceived, dataLength);
				};

				while (processActive) {
					client.PollEvents();
					Thread.Sleep(1000 / clientTickRate);
				}

				client.Stop();
			}, TaskCreationOptions.LongRunning);
		}
	}

	public class HazelBenchmark : BenchmarkNet {
		public static void Server() {
			UdpConnectionListener server = new UdpConnectionListener(new NetworkEndPoint(ip, port));

			server.Start();

			server.NewConnection += (peer, netEvent) => {
				netEvent.Connection.DataReceived += (sender, data) => {
					Connection client = (Connection)sender;

					if (data.SendOption == SendOption.Reliable) {
						Interlocked.Increment(ref serverReliableReceived);
						Interlocked.Add(ref serverReliableBytesReceived, data.Bytes.Length);
						
						if (client.State == Hazel.ConnectionState.Connected)
							client.SendBytes(messageData, SendOption.Reliable);
						
						Interlocked.Increment(ref serverReliableSent);
						Interlocked.Add(ref serverReliableBytesSent, messageData.Length);
					} else if (data.SendOption == SendOption.None) {
						Interlocked.Increment(ref serverUnreliableReceived);
						Interlocked.Add(ref serverUnreliableBytesReceived, data.Bytes.Length);

						if (client.State == Hazel.ConnectionState.Connected)
							client.SendBytes(messageData, SendOption.None);
						
						Interlocked.Increment(ref serverUnreliableSent);
						Interlocked.Add(ref serverUnreliableBytesSent, messageData.Length);
					}

					data.Recycle();
				};

				netEvent.Recycle();
			};

			while (processActive) {
				Thread.Sleep(1000 / serverTickRate);
			}

			server.Close();
		}

		public static async Task Client() {
			await Task.Factory.StartNew(() => {
				UdpClientConnection client = new UdpClientConnection(new NetworkEndPoint(ip, port));

				client.Connect();

				int reliableToSend = 0;
				int unreliableToSend = 0;
				int reliableSentCount = 0;
				int unreliableSentCount = 0;

				Task.Factory.StartNew(async() => {
					bool reliableIncremented = false;
					bool unreliableIncremented = false;

					while (processActive) {
						if (reliableToSend > 0) {
							client.SendBytes(messageData, SendOption.Reliable);
							Interlocked.Decrement(ref reliableToSend);
							Interlocked.Increment(ref reliableSentCount);
							Interlocked.Increment(ref clientsReliableSent);
							Interlocked.Add(ref clientsReliableBytesSent, messageData.Length);
						}

						if (unreliableToSend > 0) {
							client.SendBytes(messageData, SendOption.None);
							Interlocked.Decrement(ref unreliableToSend);
							Interlocked.Increment(ref unreliableSentCount);
							Interlocked.Increment(ref clientsUnreliableSent);
							Interlocked.Add(ref clientsUnreliableBytesSent, messageData.Length);
						}

						if (reliableToSend > 0 && !reliableIncremented) {
							reliableIncremented = true;
							Interlocked.Increment(ref clientsChannelsCount);
						} else if (reliableToSend == 0 && reliableIncremented) {
							reliableIncremented = false;
							Interlocked.Decrement(ref clientsChannelsCount);
						}

						if (unreliableToSend > 0 && !unreliableIncremented) {
							unreliableIncremented = true;
							Interlocked.Increment(ref clientsChannelsCount);
						} else if (unreliableToSend == 0 && unreliableIncremented) {
							unreliableIncremented = false;
							Interlocked.Decrement(ref clientsChannelsCount);
						}

						await Task.Delay(1000 / sendRate);
					}
				}, TaskCreationOptions.AttachedToParent);

				client.Disconnected += (sender, data) => {
					Interlocked.Increment(ref clientsDisconnectedCount);
					Interlocked.Exchange(ref reliableToSend, 0);
					Interlocked.Exchange(ref unreliableToSend, 0);
				};

				client.DataReceived += (sender, data) => {
					if (data.SendOption == SendOption.Reliable) {
						Interlocked.Increment(ref clientsReliableReceived);
						Interlocked.Add(ref clientsReliableBytesReceived, data.Bytes.Length);
					} else if (data.SendOption == SendOption.None) {
						Interlocked.Increment(ref clientsUnreliableReceived);
						Interlocked.Add(ref clientsUnreliableBytesReceived, data.Bytes.Length);
					}

					data.Recycle();
				};

				bool connected = false;

				while (processActive) {
					if (!connected && client.State == Hazel.ConnectionState.Connected) {
						connected = true;
						Interlocked.Increment(ref clientsConnectedCount);
						Interlocked.Exchange(ref reliableToSend, reliableMessages);
						Interlocked.Exchange(ref unreliableToSend, unreliableMessages);
					}

					Thread.Sleep(1000 / clientTickRate);
				}

				client.Close();
			}, TaskCreationOptions.LongRunning);
		}
	}

	public class PhotonBenchmark : BenchmarkNet {
		private class PhotonPeerListener : IPhotonPeerListener {
			public event Action OnConnected;
			public event Action OnDisconnected;
			public event Action<byte[]> OnReliableReceived;
			public event Action<byte[]> OnUnreliableReceived;

			public void OnMessage(object message) {
				byte[] data = (byte[])message;

				if (data[0] == messageData[0]) {
					OnReliableReceived?.Invoke(data);
					Interlocked.Increment(ref serverReliableReceived);
					Interlocked.Add(ref serverReliableBytesReceived, data.Length);
					Interlocked.Increment(ref serverReliableSent);
					Interlocked.Add(ref serverReliableBytesSent, data.Length);
				} else if (data[0] == reversedData[0]) {
					OnUnreliableReceived?.Invoke(data);
					Interlocked.Increment(ref serverUnreliableReceived);
					Interlocked.Add(ref serverUnreliableBytesReceived, data.Length);
					Interlocked.Increment(ref serverUnreliableSent);
					Interlocked.Add(ref serverUnreliableBytesSent, data.Length);
				}
			}

			public void OnStatusChanged(StatusCode statusCode) {
				switch (statusCode) {
					case StatusCode.Connect:
						OnConnected.Invoke();

						break;

					case StatusCode.Disconnect:
						OnDisconnected.Invoke();

						break;
				}
			}

			public void OnEvent(EventData netEvent) { }

			public void OnOperationResponse(OperationResponse operationResponse) { }

			public void DebugReturn(DebugLevel level, string message) { }
		}

		public static void Server() {
			PhotonPeerListener listener = new PhotonPeerListener();
			PhotonPeer server = new PhotonPeer(listener, ConnectionProtocol.Udp);

			server.Connect(ip + ":" + port, title);

			listener.OnConnected += () => {
				Thread.Sleep(Timeout.Infinite);
			};

			listener.OnDisconnected += () => {
				processFailure = true;
			};

			while (processActive) {
				server.Service();
				Thread.Sleep(1000 / serverTickRate);
			}

			server.Disconnect();
		}

		public static async Task Client() {
			await Task.Factory.StartNew(() => {
				PhotonPeerListener listener = new PhotonPeerListener();
				PhotonPeer client = new PhotonPeer(listener, ConnectionProtocol.Udp);

				client.Connect(ip + ":" + port, title);
				
				int reliableToSend = 0;
				int unreliableToSend = 0;
				int reliableSentCount = 0;
				int unreliableSentCount = 0;

				Task.Factory.StartNew(async() => {
					bool reliableIncremented = false;
					bool unreliableIncremented = false;

					while (processActive) {
						if (reliableToSend > 0) {
							client.SendMessage(messageData, true, 0, false);
							Interlocked.Decrement(ref reliableToSend);
							Interlocked.Increment(ref reliableSentCount);
							Interlocked.Increment(ref clientsReliableSent);
							Interlocked.Add(ref clientsReliableBytesSent, messageData.Length);
						}

						if (unreliableToSend > 0) {
							client.SendMessage(reversedData, false, 1, false);
							Interlocked.Decrement(ref unreliableToSend);
							Interlocked.Increment(ref unreliableSentCount);
							Interlocked.Increment(ref clientsUnreliableSent);
							Interlocked.Add(ref clientsUnreliableBytesSent, reversedData.Length);
						}

						if (reliableToSend > 0 && !reliableIncremented) {
							reliableIncremented = true;
							Interlocked.Increment(ref clientsChannelsCount);
						} else if (reliableToSend == 0 && reliableIncremented) {
							reliableIncremented = false;
							Interlocked.Decrement(ref clientsChannelsCount);
						}

						if (unreliableToSend > 0 && !unreliableIncremented) {
							unreliableIncremented = true;
							Interlocked.Increment(ref clientsChannelsCount);
						} else if (unreliableToSend == 0 && unreliableIncremented) {
							unreliableIncremented = false;
							Interlocked.Decrement(ref clientsChannelsCount);
						}

						await Task.Delay(1000 / sendRate);
					}
				}, TaskCreationOptions.AttachedToParent);

				listener.OnConnected += () => {
					Interlocked.Increment(ref clientsConnectedCount);
					Interlocked.Exchange(ref reliableToSend, reliableMessages);
					Interlocked.Exchange(ref unreliableToSend, unreliableMessages);
				};

				listener.OnDisconnected += () => {
					Interlocked.Increment(ref clientsDisconnectedCount);
					Interlocked.Exchange(ref reliableToSend, 0);
					Interlocked.Exchange(ref unreliableToSend, 0);
				};

				listener.OnReliableReceived += (data) => {
					Interlocked.Increment(ref clientsReliableReceived);
					Interlocked.Add(ref clientsReliableBytesReceived, data.Length);
				};

				listener.OnUnreliableReceived += (data) => {
					Interlocked.Increment(ref clientsUnreliableReceived);
					Interlocked.Add(ref clientsUnreliableBytesReceived, data.Length);
				};	

				while (processActive) {
					client.Service();
					Thread.Sleep(1000 / clientTickRate);
				}

				client.Disconnect();
			}, TaskCreationOptions.LongRunning);
		}
	}
}
