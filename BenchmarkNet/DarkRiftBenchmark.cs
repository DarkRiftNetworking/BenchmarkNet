/*
 *  BenchmarkNet is a console application for testing the reliable UDP networking solutions
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
 *
 *  
 *  
 *  Edited by Jamie Read to convert to DarkRift.
 *  
*/

using DarkRift;
using DarkRift.Client;
using DarkRift.Server;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

namespace BenchmarkNet
{
    public class DarkRiftBenchmark : BenchmarkNet
    {
        public static void Server()
        {
            DarkRiftServer server = new DarkRiftServer(new ServerSpawnData(IPAddress.Parse(ip), port, IPVersion.IPv4));

            server.Start();

            server.ClientManager.ClientConnected += (peer, netEvent) => {
                netEvent.Client.MessageReceived += (sender, data) => {
                    using (Message message = data.GetMessage())
                    {
                        using (DarkRiftReader reader = message.GetReader())
                        {
                            if (data.SendMode == SendMode.Reliable)
                            {
                                Interlocked.Increment(ref serverReliableReceived);
                                Interlocked.Add(ref serverReliableBytesReceived, reader.ReadBytes().Length);

                                using (DarkRiftWriter writer = DarkRiftWriter.Create(messageData.Length))
                                {
                                    writer.Write(messageData);

                                    using (Message reliableMessage = Message.Create(0, writer))
                                        data.Client.SendMessage(reliableMessage, SendMode.Reliable);
                                }

                                Interlocked.Increment(ref serverReliableSent);
                                Interlocked.Add(ref serverReliableBytesSent, messageData.Length);
                            }
                            else if (data.SendMode == SendMode.Unreliable)
                            {
                                Interlocked.Increment(ref serverUnreliableReceived);
                                Interlocked.Add(ref serverUnreliableBytesReceived, reader.ReadBytes().Length);

                                using (DarkRiftWriter writer = DarkRiftWriter.Create(messageData.Length))
                                {
                                    writer.Write(messageData);

                                    using (Message unreliableMessage = Message.Create(0, writer))
                                        data.Client.SendMessage(unreliableMessage, SendMode.Unreliable);
                                }

                                Interlocked.Increment(ref serverUnreliableSent);
                                Interlocked.Add(ref serverUnreliableBytesSent, messageData.Length);
                            }
                        }
                    }
                };
            };

            while (processActive)
            {
                server.ExecuteDispatcherTasks();
                Thread.Sleep(1000 / serverTickRate);
            }
        }

        public static async Task Client()
        {
            await Task.Factory.StartNew(() => {
                DarkRiftClient client = new DarkRiftClient();

                client.Connect(IPAddress.Parse(ip), port, IPVersion.IPv4);

                int reliableToSend = 0;
                int unreliableToSend = 0;
                int reliableSentCount = 0;
                int unreliableSentCount = 0;

                Task.Factory.StartNew(async () => {
                    bool reliableIncremented = false;
                    bool unreliableIncremented = false;

                    while (processActive)
                    {
                        if (reliableToSend > 0)
                        {
                            using (DarkRiftWriter writer = DarkRiftWriter.Create(messageData.Length))
                            {
                                writer.Write(messageData);

                                using (Message message = Message.Create(0, writer))
                                    client.SendMessage(message, SendMode.Reliable);
                            }

                            Interlocked.Decrement(ref reliableToSend);
                            Interlocked.Increment(ref reliableSentCount);
                            Interlocked.Increment(ref clientsReliableSent);
                            Interlocked.Add(ref clientsReliableBytesSent, messageData.Length);
                        }

                        if (unreliableToSend > 0)
                        {
                            using (DarkRiftWriter writer = DarkRiftWriter.Create(messageData.Length))
                            {
                                writer.Write(messageData);

                                using (Message message = Message.Create(0, writer))
                                    client.SendMessage(message, SendMode.Unreliable);
                            }

                            Interlocked.Decrement(ref unreliableToSend);
                            Interlocked.Increment(ref unreliableSentCount);
                            Interlocked.Increment(ref clientsUnreliableSent);
                            Interlocked.Add(ref clientsUnreliableBytesSent, messageData.Length);
                        }

                        if (reliableToSend > 0 && !reliableIncremented)
                        {
                            reliableIncremented = true;
                            Interlocked.Increment(ref clientsChannelsCount);
                        }
                        else if (reliableToSend == 0 && reliableIncremented)
                        {
                            reliableIncremented = false;
                            Interlocked.Decrement(ref clientsChannelsCount);
                        }

                        if (unreliableToSend > 0 && !unreliableIncremented)
                        {
                            unreliableIncremented = true;
                            Interlocked.Increment(ref clientsChannelsCount);
                        }
                        else if (unreliableToSend == 0 && unreliableIncremented)
                        {
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

                client.MessageReceived += (sender, data) => {
                    using (Message message = data.GetMessage())
                    {
                        using (DarkRiftReader reader = message.GetReader())
                        {
                            if (data.SendMode == SendMode.Reliable)
                            {
                                Interlocked.Increment(ref clientsReliableReceived);
                                Interlocked.Add(ref clientsReliableBytesReceived, reader.ReadBytes().Length);
                            }
                            else if (data.SendMode == SendMode.Unreliable)
                            {
                                Interlocked.Increment(ref clientsUnreliableReceived);
                                Interlocked.Add(ref clientsUnreliableBytesReceived, reader.ReadBytes().Length);
                            }
                        }
                    }
                };

                bool connected = false;

                while (processActive)
                {
                    if (!connected && client.Connected)
                    {
                        connected = true;
                        Interlocked.Increment(ref clientsConnectedCount);
                        Interlocked.Exchange(ref reliableToSend, reliableMessages);
                        Interlocked.Exchange(ref unreliableToSend, unreliableMessages);
                    }

                    Thread.Sleep(1000 / clientTickRate);
                }
            }, TaskCreationOptions.LongRunning);
        }
    }
}
