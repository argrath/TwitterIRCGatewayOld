﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using Misuzilla.Net.Irc;

namespace Misuzilla.Applications.TwitterIrcGateway
{
    public class Connection : ConnectionBase
    {
        public User TwitterUser { get; private set; }

        public Connection(Server server, TcpClient tcpClient) : base(server, tcpClient)
        {
            _Counter.Increment(ref _Counter.Connection);
        }
        ~Connection()
        {
            _Counter.Decrement(ref _Counter.Connection);
        }

        protected override AuthenticateResult OnAuthenticate(UserInfo userInfo)
        {
            AuthenticateResult authResult = CurrentServer.Authentication.Authenticate(CurrentServer, this, userInfo);
            if (authResult is TwitterAuthenticateResult)
            {
                TwitterUser = ((TwitterAuthenticateResult) authResult).User;
            }
            return authResult;
        }

        protected override void OnAuthenticateSucceeded()
        {
            Session session = CurrentServer.GetOrCreateSession(TwitterUser);
            session.Attach(this);
        }

        protected override void OnAuthenticateFailed(AuthenticateResult authenticateResult)
        {
        }
    }
}
