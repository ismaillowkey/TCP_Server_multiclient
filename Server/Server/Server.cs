using System;
using System.Windows.Forms;

namespace Server
{
    public partial class Server : Form
    {
        private TCPServer telnet;

        public Server()
        {
            InitializeComponent();
            telnet = new TCPServer(localaddrMaskedTextBox.Text, Convert.ToInt32(portTextBox.Text));

            telnet.StatusServer += Telnet_StatusServer;
            telnet.OnConnected += Telnet_OnConnected;
            telnet.OnDisconnected += Telnet_OnDisconnected;
            telnet.MessageReceived += Telnet_MessageReceived;
            telnet.ErrorEvent += Telnet_ErrorEvent;
        }

        private void Telnet_StatusServer(bool StatusServer, string message)
        {
            startButton.Invoke((MethodInvoker)delegate
            {
                if (StatusServer)
                {
                    startButton.Text = "Stop";
                }
                else
                {
                    startButton.Text = "Start";
                }
            });
        }

        private void Telnet_OnConnected(TCPServer.MyClient myClient)
        {
            logTextBox.Invoke((MethodInvoker)delegate
            {
                if (logTextBox.Text.Length > 0)
                {
                    logTextBox.AppendText(Environment.NewLine);
                }
                logTextBox.AppendText("Client : " + myClient.client.Client.RemoteEndPoint + " Connected");
            });
        }

        private void Telnet_OnDisconnected(TCPServer.MyClient myClient)
        {
            logTextBox.Invoke((MethodInvoker)delegate
            {
                if (logTextBox.Text.Length > 0)
                {
                    logTextBox.AppendText(Environment.NewLine);
                }
                logTextBox.AppendText("Client : Disconnected");
            });
        }


        private void Telnet_MessageReceived(TCPServer.MyClient myClient, string message)
        {
            logTextBox.Invoke((MethodInvoker)delegate
            {
                if (message == null)
                {
                    logTextBox.Clear();
                }
                else
                {
                    if (logTextBox.Text.Length > 0)
                    {
                        logTextBox.AppendText(Environment.NewLine);
                    }
                    logTextBox.AppendText(message);
                }
            });
        }

        private void Telnet_ErrorEvent(TCPServer.MyClient myClient, string message)
        {

        }

        private void StartButton_Click(object sender, EventArgs e)
        {
            telnet.StartServer();
        }


        private void SendTextBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter)
            {
                e.Handled = true;
                e.SuppressKeyPress = true;
                if (sendTextBox.Text.Length > 0)
                {
                    string msg = sendTextBox.Text;
                    sendTextBox.Clear();
                    //telnet.LogWrite("<- Server (You) -> " + msg);
                    telnet.SendBackMessage(msg);
                }
            }
        }


        private void DisconnectButton_Click(object sender, EventArgs e)
        {
            telnet.StopServer();
        }

        private void Server_FormClosing(object sender, FormClosingEventArgs e)
        {
            telnet.StopServer();
        }

        private void ClearButton_Click(object sender, EventArgs e)
        {
            logTextBox.Invoke((MethodInvoker)delegate
            {
                logTextBox.Clear();
            });
        }


    }
}
