using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;

namespace Misuzilla.Applications.TwitterIrcGateway
{
    public class UserInfo : MarshalByRefObject
    {
        public String Nick { get; set; }
        public String UserName { get; set; }
        public String RealName { get; set; }
        public String Password { get; set; }
        public IPEndPoint EndPoint { get; set; }
        public String ClientHost
        {
            get
            {
                return String.Format("{0}!{1}@{2}", Nick, UserName, EndPoint.Address);
            }
        }

        public UserInfo()
        {
        }

        public UserInfo(String nick, String userName, IPEndPoint endPoint, String realName, String password)
        {
            Nick = nick;
            UserName = userName;
            EndPoint = endPoint;
            RealName = realName;
            Password = password;
        }

        public override string ToString()
        {
            return String.Format("UserInfo: Nick={0}; UserName={1}; HostName={2}; RealName={3}", Nick, UserName, ClientHost, RealName);
        }
    }
}
