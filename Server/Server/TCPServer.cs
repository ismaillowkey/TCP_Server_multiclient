using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Server
{
    class TCPServer
    {
        private bool active;
        private Thread listener;
        private long id = 0;
        private string ipaddress = "0.0.0.0";
        private int portTCP = 23;

        public struct MyClient
        {
            public long id;
            public TcpClient client;
            public NetworkStream stream;
            public byte[] buffer;
            public StringBuilder data;
            public EventWaitHandle handle;
        };

        public delegate void StatusServerHandler(bool StatusServer, String message);
        public delegate void OnConnectedHandler(MyClient myClient);
        public delegate void OnDisconnectedHandler(MyClient myClient);
        public delegate void MessageReceivedHandler(MyClient myClient, String message);
        public delegate void ErrorEventHandler(MyClient myClient, String message);

        /// <summary>
        /// Status Server
        /// </summary>
        public event StatusServerHandler StatusServer;
        /// <summary>
        /// When client connected to this server
        /// </summary>
        public event OnConnectedHandler OnConnected;
        /// <summary>
        /// When client disconnected to this server
        /// </summary>
        public event OnDisconnectedHandler OnDisconnected;
        /// <summary>
        /// When Message Received from client
        /// </summary>
        public event MessageReceivedHandler MessageReceived;
        /// <summary>
        /// When Error
        /// </summary>
        public event ErrorEventHandler ErrorEvent;


        private ConcurrentDictionary<long, MyClient> list = new ConcurrentDictionary<long, MyClient>();
        private Task send { get; set; }
        private Thread disconnect { get; set; }
        private bool exit { get; set; }

        public TCPServer(String IPAddress, int Port)
        {
            this.ipaddress = IPAddress;
            this.portTCP = Port;
            active = false;
            exit = false;
            listener = null;
            disconnect = null;
            send = null;
        }

        private void Active(bool status)
        {
            if (!exit)
            {
                active = status;
            }
        }

        private void Read(IAsyncResult result)
        {
            MyClient obj = (MyClient)result.AsyncState;
            int bytes = 0;
            if (obj.client.Connected)
            {
                try
                {
                    bytes = obj.stream.EndRead(result);
                }
                catch (IOException e)
                {
                    //LogWrite(string.Format("[/ {0} /]", e.Message));
                    ErrorEvent(obj, e.Message);
                }
                catch (ObjectDisposedException e)
                {
                    //LogWrite(string.Format("[/ {0} /]", e.Message));
                    ErrorEvent(obj, e.Message);
                }
            }
            if (bytes > 0)
            {
                obj.data.AppendFormat("{0}", Encoding.UTF8.GetString(obj.buffer, 0, bytes));
                bool dataAvailable = false;
                try
                {
                    dataAvailable = obj.stream.DataAvailable;
                }
                catch (IOException e)
                {
                    //LogWrite(string.Format("[/ {0} /]", e.Message));
                    ErrorEvent(obj, e.Message);
                }
                catch (ObjectDisposedException e)
                {
                    //LogWrite(string.Format("[/ {0} /]", e.Message));
                    ErrorEvent(obj, e.Message);
                }
                if (dataAvailable)
                {
                    try
                    {
                        obj.stream.BeginRead(obj.buffer, 0, obj.buffer.Length, new AsyncCallback(Read), obj);
                    }
                    catch (IOException e)
                    {
                        //LogWrite(string.Format("[/ {0} /]", e.Message));
                        ErrorEvent(obj, e.Message);
                        obj.handle.Set();
                    }
                    catch (ObjectDisposedException e)
                    {
                        //LogWrite(string.Format("[/ {0} /]", e.Message));
                        ErrorEvent(obj, e.Message);
                        obj.handle.Set();
                    }
                }
                else
                {
                    // when data received
                    string msg = obj.data.ToString();
                    MessageReceived(obj, msg);
                    if (send == null || send.IsCompleted)
                    {
                        send = Task.Factory.StartNew(() => Send(msg, obj.id));
                    }
                    else
                    {
                        send.ContinueWith(antecendent => Send(msg, obj.id));
                    }
                    obj.data.Clear();
                    obj.handle.Set();
                }
            }
            else
            {
                obj.client.Close();
                obj.handle.Set();
            }
        }

        private void Connection(MyClient obj)
        {
            list.TryAdd(obj.id, obj);
            OnConnected(obj);
            if (obj.stream.CanRead && obj.stream.CanWrite)
            {
                while (obj.client.Connected)
                {
                    try
                    {
                        obj.stream.BeginRead(obj.buffer, 0, obj.buffer.Length, new AsyncCallback(Read), obj);
                        obj.handle.WaitOne();
                    }
                    catch (IOException e)
                    {
                        //LogWrite(string.Format("[/ {0} /]", e.Message)); 
                        ErrorEvent(obj, e.Message);
                    }
                    catch (ObjectDisposedException e)
                    {
                        //LogWrite(string.Format("[/ {0} /]", e.Message));
                        ErrorEvent(obj, e.Message);
                    }
                }
            }
            else
            {
                //LogWrite(string.Format("[/ Client {0} stream cannot read/write /]", obj.id));
                ErrorEvent(obj, "Client {0} stream cannot read/write");
            }

            obj.client.Close();
            list.TryRemove(obj.id, out MyClient tmp);
            OnDisconnected(obj);

            //LogWrite(string.Format("[/ Client {0} connection closed /]", obj.id));
        }

        private void Listener(IPAddress localaddr, int port)
        {
            MyClient obj = new MyClient();
            try
            {
                TcpListener listener = new TcpListener(localaddr, port);
                listener.Start();
                Active(true);
                while (active)
                {
                    if (listener.Pending())
                    {
                        obj.id = id;
                        obj.client = listener.AcceptTcpClient();
                        obj.stream = obj.client.GetStream();
                        obj.buffer = new byte[obj.client.ReceiveBufferSize];
                        obj.data = new StringBuilder();
                        obj.handle = new EventWaitHandle(false, EventResetMode.AutoReset);
                        Thread th = new Thread(() => Connection(obj))
                        {
                            IsBackground = true
                        };
                        th.Start();
                        id++;
                    }
                    else
                    {
                        Thread.Sleep(500);
                    }
                }
                listener.Server.Close();
                Active(false);
            }
            catch (SocketException e)
            {
                //LogWrite(string.Format("[/ {0} /]", e.Message));
                ErrorEvent(obj, e.Message);
            }
        }

        private void Write(IAsyncResult result)
        {
            MyClient obj = (MyClient)result.AsyncState;
            if (obj.client.Connected)
            {
                try
                {
                    obj.stream.EndWrite(result);
                }
                catch (IOException e)
                {
                    //LogWrite(string.Format("[/ {0} /]", e.Message));
                    ErrorEvent(obj, e.Message);
                }
                catch (ObjectDisposedException e)
                {
                    //LogWrite(string.Format("[/ {0} /]", e.Message));
                    ErrorEvent(obj, e.Message);
                }
            }
        }

        private void Send(string msg, long id = -1)
        {
            byte[] buffer = Encoding.UTF8.GetBytes(msg);
            foreach (KeyValuePair<long, MyClient> obj in list)
            {
                if (id != obj.Value.id)
                {
                    try
                    {
                        obj.Value.stream.BeginWrite(buffer, 0, buffer.Length, new AsyncCallback(Write), obj.Value);
                    }
                    catch (IOException e)
                    {
                        //LogWrite(string.Format("[/ {0} /]", e.Message));
                        ErrorEvent(obj.Value, e.Message);
                    }
                    catch (ObjectDisposedException e)
                    {
                        //LogWrite(string.Format("[/ {0} /]", e.Message));
                        ErrorEvent(obj.Value, e.Message);
                    }
                }
            }
        }

        private void Disconnect()
        {
            foreach (KeyValuePair<long, MyClient> obj in list)
            {
                obj.Value.client.Close();
            }
        }

        /// <summary>
        /// For Start Server
        /// </summary>
        public void StartServer()
        {
            if (active)
            {
                active = false;
            }
            else
            {
                if (listener == null || !listener.IsAlive)
                {
                    bool localaddrResult = IPAddress.TryParse(ipaddress, out IPAddress localaddr);
                    if (!localaddrResult)
                    {
                        //LogWrite("[/ Address is not valid /]");
                        StatusServer(false, "Address is not valid");
                    }
                    bool portResult = int.TryParse(Convert.ToString(portTCP), out int port);
                    if (!portResult)
                    {
                        //LogWrite("[/ Port is not valid /]");
                        StatusServer(false, "Port is not valid");
                    }
                    else if (port < 0 || port > 65535)
                    {
                        portResult = false;
                        //LogWrite("[/ Port is out of range /]");
                        StatusServer(false, "Port is out of range");
                    }
                    if (localaddrResult && portResult)
                    {
                        listener = new Thread(() => Listener(localaddr, port))
                        {
                            IsBackground = true
                        };
                        listener.Start();
                        StatusServer(true, "Server Started");
                    }
                }
            }
        }

        /// <summary>
        /// For Stop Server
        /// </summary>
        public void StopServer()
        {
            exit = true;
            active = false;
            if (disconnect == null || !disconnect.IsAlive)
            {
                disconnect = new Thread(() => Disconnect())
                {
                    IsBackground = true
                };
                disconnect.Start();
                StatusServer(false, "Stop Server");
            }
        }

        /// <summary>
        /// Send message back to client
        /// </summary>
        /// <param name="message"></param>
        public void SendBackMessage(string message)
        {
            if (send == null || send.IsCompleted)
            {
                send = Task.Factory.StartNew(() => Send(message));
            }
            else
            {
                send.ContinueWith(antecendent => Send(message));
            }
        }
    }
}
