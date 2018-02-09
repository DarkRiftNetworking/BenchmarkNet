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

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace BenchmarkNet
{
    /// <summary>
    ///     Testing framework.
    /// </summary>
    public class BenchmarkNet
    {
        protected const string title = "BenchmarkNet";
        protected const string version = "1.02";
        protected const string ip = "127.0.0.1";
        protected static ushort port = 0;
        protected static ushort maxClients = 0;
        protected static int sendRate = 0;
        protected static int reliableMessages = 0;
        protected static int unreliableMessages = 0;
        protected static string message = "";
        protected static byte[] messageData;
        protected static bool processActive = false;
        protected static bool processCompleted = false;
        protected static bool processOverload = false;
        protected static Thread serverThread;
        protected static volatile int clientsStartedCount = 0;
        protected static volatile int clientsConnectedCount = 0;
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

        private static void Main()
        {
            Console.Title = title;
            Console.SetIn(new StreamReader(Console.OpenStandardInput(8192), Console.InputEncoding, false, bufferSize: 1024));
            Console.WriteLine("Welcome to " + title + "!");
            Console.WriteLine("Version " + version);
            Console.WriteLine(Environment.NewLine + "Source code is available on GitHub (https://github.com/DarkRiftNetworking/BenchmarkNet)");
            Console.WriteLine("If you have any questions, contact me (jamie@darkriftnetworking.com)");

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

            messageData = Encoding.ASCII.GetBytes(message);

            Console.CursorVisible = false;
            Console.Clear();

            processActive = true;

            serverThread = new Thread(DarkRiftBenchmark.Server);
            serverThread.Start();
            Thread.Sleep(100);

            Task infoTask = Info();
            Task superviseTask = Supervise();
            Task spawnTask = Spawn();

            Console.ReadKey();
            processActive = false;
            Environment.Exit(0);
        }

        private static async Task Info()
        {
            await Task.Factory.StartNew(() => {
                int spinnerTimer = 0;
                int spinnerSequence = 0;
                string spinner = "";
                Stopwatch stopwatch = new Stopwatch();

                stopwatch.Start();

                while (processActive)
                {
                    Console.CursorVisible = false;
                    Console.SetCursorPosition(0, 0);
                    Console.WriteLine("Benchmarking DarkRift...");
                    Console.WriteLine(maxClients + " clients, " + reliableMessages + " reliable and " + unreliableMessages + " unreliable messages per client, " + messageData.Length + " bytes per message, " + sendRate + " messages per second");

                    Console.WriteLine(Environment.NewLine + "Server status: " + (processOverload ? "Overload" : (processCompleted && serverThread.IsAlive ? "Completed" : (serverThread.IsAlive ? "Running" : "Failure"))));
                    Console.WriteLine("Clients status: " + clientsStartedCount + " started, " + clientsConnectedCount + " connected, " + clientsDisconnectedCount + " dropped");
                    Console.WriteLine("Clients sent -> Reliable: " + clientsReliableSent + " messages (" + clientsReliableBytesSent + " bytes), Unreliable: " + clientsUnreliableSent + " messages (" + clientsUnreliableBytesSent + " bytes)");
                    Console.WriteLine("Server received <- Reliable: " + serverReliableReceived + " messages (" + serverReliableBytesReceived + " bytes), Unreliable: " + serverUnreliableReceived + " messages (" + serverUnreliableBytesReceived + " bytes)");
                    Console.WriteLine("Server sent -> Reliable: " + serverReliableSent + " messages (" + serverReliableBytesSent + " bytes), Unreliable: " + serverUnreliableSent + " messages (" + serverUnreliableBytesSent + " bytes)");
                    Console.WriteLine("Clients received <- Reliable: " + clientsReliableReceived + " messages (" + clientsReliableBytesReceived + " bytes), Unreliable: " + clientsUnreliableReceived + " messages (" + clientsUnreliableBytesReceived + " bytes)");
                    Console.WriteLine("Total - Reliable: " + ((ulong)clientsReliableSent + (ulong)serverReliableReceived + (ulong)serverReliableSent + (ulong)clientsReliableReceived) + " messages (" + ((ulong)clientsReliableBytesSent + (ulong)serverReliableBytesReceived + (ulong)serverReliableBytesSent + (ulong)clientsReliableBytesReceived) + " bytes), Unreliable: " + ((ulong)clientsUnreliableSent + (ulong)serverUnreliableReceived + (ulong)serverUnreliableSent + (ulong)clientsUnreliableReceived) + " messages (" + ((ulong)clientsUnreliableBytesSent + (ulong)serverUnreliableBytesReceived + (ulong)serverUnreliableBytesSent + (ulong)clientsUnreliableBytesReceived) + " bytes)");
                    Console.WriteLine("Expected - Reliable: " + (maxClients * (ulong)reliableMessages * 4) + " messages (" + (maxClients * (ulong)reliableMessages * (ulong)messageData.Length * 4) + " bytes), Unreliable: " + (maxClients * (ulong)unreliableMessages * 4) + " messages (" + (maxClients * (ulong)unreliableMessages * (ulong)messageData.Length * 4) + " bytes)");
                    Console.WriteLine("Elapsed time: " + stopwatch.Elapsed.Hours.ToString("00") + ":" + stopwatch.Elapsed.Minutes.ToString("00") + ":" + stopwatch.Elapsed.Seconds.ToString("00"));

                    if (spinnerTimer >= 10)
                    {
                        spinnerSequence++;
                        spinnerTimer = 0;
                    }
                    else
                    {
                        spinnerTimer++;
                    }

                    switch (spinnerSequence % 4)
                    {
                        case 0:
                            spinner = "/";
                            break;
                        case 1:
                            spinner = "—";
                            break;
                        case 2:
                            spinner = "\\";
                            break;
                        case 3:
                            spinner = "|";
                            break;
                    }

                    Console.WriteLine(Environment.NewLine + "Press any key to stop the process " + spinner);
                    Thread.Sleep(15);
                }

                if (!processActive && processCompleted)
                {
                    Console.SetCursorPosition(0, Console.CursorTop - 1);
                    Console.WriteLine("Process completed! Press any key to exit...");
                }

                stopwatch.Stop();
            }, TaskCreationOptions.LongRunning);
        }

        private static async Task Supervise()
        {
            await Task.Factory.StartNew(() => {
                decimal lastData = 0;

                while (processActive)
                {
                    Thread.Sleep(1000);

                    decimal currentData = ((decimal)serverReliableSent + (decimal)serverReliableReceived + (decimal)serverUnreliableSent + (decimal)serverUnreliableReceived + (decimal)clientsReliableSent + (decimal)clientsReliableReceived + (decimal)clientsUnreliableSent + (decimal)clientsUnreliableReceived);

                    if (currentData == lastData)
                    {
                        if (currentData != 0 && ((currentData / (maxClients * ((decimal)reliableMessages + (decimal)unreliableMessages) * 4)) * 100) < 90)
                            processOverload = true;

                        processCompleted = true;
                        Thread.Sleep(100);
                        processActive = false;

                        break;
                    }

                    lastData = currentData;
                }
            }, TaskCreationOptions.LongRunning);
        }

        private static async Task Spawn()
        {
            await Task.Factory.StartNew(() => {
                List<Task> clients = new List<Task>();

                for (int i = 0; i < maxClients; i++)
                {
                    if (!processActive)
                        break;

                    clients.Add(DarkRiftBenchmark.Client());

                    Interlocked.Increment(ref clientsStartedCount);
                    Thread.Sleep(15);
                }
            }, TaskCreationOptions.LongRunning);
        }
    }
}
