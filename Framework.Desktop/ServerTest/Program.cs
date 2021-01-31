using Honjo.Framework.Network;
using System;
using System.Net;
using System.Threading;

namespace ServerTest
{
    internal class ServerTest
    {
        internal const int MAX_CLIENTS = 200, PORT = 5000, MAX_PING = 500;
        internal const Protocol PROTOCOL = Protocol.TCP;
        internal static readonly IPAddress SERVER_ADDRESS = IPAddress.Loopback;

        internal static void Main(string[] args)
        {
            ManyClientsPing();
        }

        internal static void ManyClientsPing()
        {
            while (true)
            {
                Client[] array = new Client[MAX_CLIENTS];
                object array_lock = new object();
                for (int i = 0; i < MAX_CLIENTS - 1; i++)
                {
                    //Console.WriteLine("Connecting new client");
                    new Thread(() =>
                    {
                        Client c = new Client(SERVER_ADDRESS, PROTOCOL, PORT);
                        lock (array_lock)
                            array[i] = c;
                    }).Start();
                }
                Thread.Sleep(60000);
                Console.WriteLine("Pinging...");
                for (int i = 0; i < array.Length - 1; i++)
                {
                    new Thread(() =>
                    {
                        Client c = array[i];
                        if (c == null)
                            return;
                        for (int j = 0; j < MAX_PING; j++)
                        {
                            Console.WriteLine("PINGING...");
                            c.Ping();
                            Thread.Sleep(20);
                        }
                        Console.WriteLine("WILL NOW BREAK");
                        //c.Disconnect();
                    }).Start();
                }
                Thread.Sleep(15000);
                break;
            }
        }
    }
}
