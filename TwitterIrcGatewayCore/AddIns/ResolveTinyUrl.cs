using System;
using System.Collections.Generic;
using System.Text;

namespace Misuzilla.Applications.TwitterIrcGateway.AddIns
{
    class ResolveTinyUrl : AddInBase
    {
        public override void Initialize()
        {
            Session.PostFilterProcessTimelineStatus += new EventHandler<TimelineStatusEventArgs>(Session_PostFilterProcessTimelineStatus);
        }

        void Session_PostFilterProcessTimelineStatus(object sender, TimelineStatusEventArgs e)
        {
            // TinyURL
            e.Text = (Server.ResolveTinyUrl) ? Utility.ResolveTinyUrlInMessage(e.Text) : e.Text;
        }
    }
}
