using System;
using System.Collections.Generic;
using System.Text;
using System.Diagnostics;

namespace Misuzilla.Applications.TwitterIrcGateway.AddIns
{
    class RemoveRedundantSuffix : AddInBase
    {
        private Dictionary<Int32, LinkedList<String>> _lastStatusFromFriends;
        
        public override void Initialize()
        {
            Session.PostFilterProcessTimelineStatus += new EventHandler<TimelineStatusEventArgs>(Session_PostFilterProcessTimelineStatus);
            Session.ConfigChanged += new EventHandler<EventArgs>(Session_ConfigChanged);
            
            if (Session.Config.EnableRemoveRedundantSuffix)
                _lastStatusFromFriends = new Dictionary<int, LinkedList<string>>();
        }

        void Session_ConfigChanged(object sender, EventArgs e)
        {
            if (_lastStatusFromFriends == null && Session.Config.EnableRemoveRedundantSuffix)
            {
                _lastStatusFromFriends = new Dictionary<int, LinkedList<string>>();
            }
            else
            {
                _lastStatusFromFriends = null;
            }
        }

        void Session_PostFilterProcessTimelineStatus(object sender, TimelineStatusEventArgs e)
        {
            // Remove Redundant Suffixes
            if (Session.Config.EnableRemoveRedundantSuffix)
            {
                if (!_lastStatusFromFriends.ContainsKey(e.Status.User.Id))
                {
                    _lastStatusFromFriends[e.Status.User.Id] = new LinkedList<string>();
                }
                LinkedList<String> lastStatusTextsByUId = _lastStatusFromFriends[e.Status.User.Id];
                String suffix = Utility.DetectRedundantSuffix(e.Text, lastStatusTextsByUId);
                lastStatusTextsByUId.AddLast(e.Text);
                if (lastStatusTextsByUId.Count > 5)
                {
                    lastStatusTextsByUId.RemoveFirst();
                }
                if (!String.IsNullOrEmpty(suffix))
                {
                    Trace.WriteLine("Remove Redundant suffix: " + suffix);
                    e.Text = e.Text.Substring(0, e.Text.Length - suffix.Length);
                }
            }
        }
    }
}
