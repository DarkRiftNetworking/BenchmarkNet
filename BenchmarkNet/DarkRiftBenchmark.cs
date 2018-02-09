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
            using (DarkRiftServer server = new DarkRiftServer(new ServerSpawnData(IPAddress.Parse(ip), port, IPVersion.IPv4)))
            {
                server.Start();

                server.ClientManager.ClientConnected += (sender, e) =>
                {
                    e.Client.MessageReceived += (sender2, e2) =>
                    {
                        using (Message message = e2.GetMessage())
                        using (DarkRiftReader reader = message.GetReader())
                        {
                            if (e2.SendMode == SendMode.Reliable)
                            {
                                Interlocked.Increment(ref serverReliableReceived);
                                using (DarkRiftWriter writer = DarkRiftWriter.Create(messageData.Length))
                                {
                                    writer.Write(messageData);         //TODO use WriteRaw once exposed

                                    using (Message outMessage = Message.Create(0, writer))
                                        e2.Client.SendMessage(message, SendMode.Reliable);
                                }
                                Interlocked.Increment(ref serverReliableSent);
                                Interlocked.Add(ref serverReliableBytesSent, messageData.Length);
                                Interlocked.Add(ref serverReliableBytesReceived, reader.Length);
                            }
                            else
                            {
                                Interlocked.Increment(ref serverUnreliableReceived);
                                using (DarkRiftWriter writer = DarkRiftWriter.Create(messageData.Length))
                                {
                                    writer.Write(messageData);         //TODO use WriteRaw once exposed

                                    using (Message outMessage = Message.Create(0, writer))
                                        e2.Client.SendMessage(message, SendMode.Unreliable);
                                }
                                Interlocked.Increment(ref serverUnreliableSent);
                                Interlocked.Add(ref serverUnreliableBytesSent, messageData.Length);
                                Interlocked.Add(ref serverUnreliableBytesReceived, reader.Length);
                            }
                        }
                    };
                };

                while (processActive)
                {
                    server.ExecuteDispatcherTasks();
                }
            }
        }

        public static async Task Client()
        {
            await Task.Factory.StartNew(async () => {
                using (DarkRiftClient client = new DarkRiftClient())
                {
                    client.Connect(IPAddress.Parse(ip), port, IPVersion.IPv4);

                    int reliableToSend = 0;
                    int unreliableToSend = 0;
                    int reliableSentCount = 0;
                    int unreliableSentCount = 0;

                    Interlocked.Increment(ref clientsConnectedCount);
                    Interlocked.Exchange(ref reliableToSend, reliableMessages);
                    Interlocked.Exchange(ref unreliableToSend, unreliableMessages);

                    client.MessageReceived += (sender, e) =>
                    {
                        using (Message message = e.GetMessage())
                        using (DarkRiftReader reader = message.GetReader())
                        {
                            if (e.SendMode == SendMode.Reliable)
                            {
                                Interlocked.Increment(ref clientsReliableReceived);
                                Interlocked.Add(ref clientsReliableBytesReceived, reader.Length);
                            }
                            else
                            {
                                Interlocked.Increment(ref clientsUnreliableReceived);
                                Interlocked.Add(ref clientsUnreliableBytesReceived, reader.Length);
                            }
                        }
                    };

                    client.Disconnected += (sender, e) =>
                    {
                        Interlocked.Increment(ref clientsDisconnectedCount);
                    };

                    while (processActive)
                    {
                        if (reliableToSend > 0)
                        {
                            using (DarkRiftWriter writer = DarkRiftWriter.Create(messageData.Length))
                            {
                                writer.WriteRaw(messageData, 0, messageData.Length);

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
                                writer.WriteRaw(messageData, 0, messageData.Length);

                                using (Message message = Message.Create(0, writer))
                                    client.SendMessage(message, SendMode.Unreliable);
                            }
                            Interlocked.Decrement(ref unreliableToSend);
                            Interlocked.Increment(ref unreliableSentCount);
                            Interlocked.Increment(ref clientsUnreliableSent);
                            Interlocked.Add(ref clientsUnreliableBytesSent, messageData.Length);
                        }

                        await Task.Delay(1000 / sendRate);
                    }

                    client.Disconnect();
                }
            }, TaskCreationOptions.LongRunning);
        }
    }
}
