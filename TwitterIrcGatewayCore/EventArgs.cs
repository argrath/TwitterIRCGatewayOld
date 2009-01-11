using System;
using System.Collections.Generic;
using System.Text;
using System.Net.Sockets;
using System.IO;
using Misuzilla.Net.Irc;

namespace Misuzilla.Applications.TwitterIrcGateway
{
    public class MessageReceivedEventArgs : CancelableEventArgs
    {
        public IRCMessage Message;
        public TcpClient Client;
        public StreamWriter Writer;
        public MessageReceivedEventArgs(IRCMessage msg, StreamWriter sw, TcpClient tcpClient)
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

    public abstract class CancelableEventArgs : EventArgs
    {
        public Boolean Cancel { get; set; }
    }

    public class TimelineStatusesEventArgs : CancelableEventArgs
    {
        public Statuses Statuses { get; private set; }
        public Boolean IsFirstTime { get; set; }
        
        public TimelineStatusesEventArgs(Statuses statuses, Boolean isFirstTime)
        {
            Statuses = statuses;
            IsFirstTime = isFirstTime;
        }
    }
    
    public class TimelineStatusEventArgs : CancelableEventArgs
    {
        public Status Status { get; private set; }
        public String Text { get; set; }
        public String IRCMessageType { get; set; }
        
        public TimelineStatusEventArgs(Status status) : this(status, status.Text, "")
        {
        }
        public TimelineStatusEventArgs(Status status, String text, String ircMessageType)
        {
            Status = status;
            Text = text;
            IRCMessageType = ircMessageType;
        }
    }

    public class StatusUpdateEventArgs : CancelableEventArgs
    {
        public PrivMsgMessage ReceivedMessage { get; set; }
        public String Text { get; set; }
        public Status CreatedStatus { get; set; }
        
        public StatusUpdateEventArgs(PrivMsgMessage receivedMessage, String text)
        {
            ReceivedMessage = receivedMessage;
            Text = text;
        }
    }
}
