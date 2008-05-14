using System;
using System.Collections.Generic;
using System.Text;
using System.Net.Sockets;
using System.IO;
using Misuzilla.Net.Irc;

namespace Misuzilla.Applications.TwitterIrcGateway
{
    public class MessageRecievedEventArgs : EventArgs
    {
        public IRCMessage Message;
        public TcpClient Client;
        public StreamWriter Writer;
        public MessageRecievedEventArgs(IRCMessage msg, StreamWriter sw, TcpClient tcpClient)
        {
            Writer = sw;
            Client = tcpClient;
            Message = msg;
        }
    }

    public class SessionStartedEventArgs : EventArgs
    {
        public String UserName;
        public SessionStartedEventArgs(String userName)
        {
            UserName = userName;
        }
    }
}
