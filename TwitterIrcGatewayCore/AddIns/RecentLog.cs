using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Misuzilla.Net.Irc;

namespace Misuzilla.Applications.TwitterIrcGateway.AddIns
{
    public class RecentLog : AddInBase
    {
        private Dictionary<String, List<Status>> _recentStatuses;
        private const Int32 MaxCount = 10;
        
        public override void Initialize()
        {
            base.Initialize();

            _recentStatuses = new Dictionary<string, List<Status>>();
            CurrentSession.ConnectionAttached += CurrentSession_ConnectionAttached;
            CurrentSession.PostSendGroupMessageTimelineStatus += new EventHandler<TimelineStatusGroupEventArgs>(CurrentSession_PreSendGroupMessageTimelineStatus);
        }

        void CurrentSession_PreSendGroupMessageTimelineStatus(object sender, TimelineStatusGroupEventArgs e)
        {
            if (!_recentStatuses.ContainsKey(e.Group.Name))
                _recentStatuses[e.Group.Name] = new List<Status>();

            _recentStatuses[e.Group.Name].Add(e.Status);
            if (_recentStatuses[e.Group.Name].Count > MaxCount)
            {
                _recentStatuses[e.Group.Name].RemoveAt(0);
            }
        }

        public override void Uninitialize()
        {
            CurrentSession.ConnectionAttached -= CurrentSession_ConnectionAttached;
            base.Uninitialize();
        }

        void CurrentSession_ConnectionAttached(object sender, ConnectionAttachEventArgs e)
        {
            foreach (Group group in CurrentSession.Groups.Values.Where(g => g.IsJoined && !g.IsSpecial))
            {
                foreach (Status status in _recentStatuses[group.Name])
                {
                    e.Connection.Send(new NoticeMessage(group.Name,
                                                        String.Format("{0}: {1}",
                                                                      status.CreatedAt.ToString("HH:mm"),
                                                                      status.Text))
                                          {SenderNick = status.User.ScreenName});
                }
            }
        }
    }
}
