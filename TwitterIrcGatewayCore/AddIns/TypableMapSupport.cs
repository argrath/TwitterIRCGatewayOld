using System;
using System.Collections.Generic;
using System.Text;
using Misuzilla.Applications.TwitterIrcGateway.AddIns.TypableMap;

namespace Misuzilla.Applications.TwitterIrcGateway.AddIns
{
    class TypableMapSupport : AddInBase
    {
        private TypableMapCommandProcessor _typableMapCommands;
        
        public override void Initialize()
        {
            Session.PreSendUpdateStatus += new EventHandler<StatusUpdateEventArgs>(Session_PreSendUpdateStatus);
            Session.PreSendMessageTimelineStatus += new EventHandler<TimelineStatusEventArgs>(Session_PreSendMessageTimelineStatus);
            Session.ConfigChanged += new EventHandler<EventArgs>(Session_ConfigChanged);

            if (Session.Config.EnableTypableMap)
                _typableMapCommands = new TypableMapCommandProcessor(Session.TwitterService, Session, Session.Config.TypableMapKeySize);
        }

        void Session_PreSendMessageTimelineStatus(object sender, TimelineStatusEventArgs e)
        {
            // TypableMap
            if (Session.Config.EnableTypableMap)
            {
                String typableMapId = _typableMapCommands.TypableMap.Add(e.Status);
                // TypableMapKeyColorNumber = -1 の場合には色がつかなくなる
                if (Session.Config.TypableMapKeyColorNumber < 0)
                    e.Text = String.Format("{0} ({1})", e.Text, typableMapId);
                else
                    e.Text = String.Format("{0} \x0003{1}({2})", e.Text, Session.Config.TypableMapKeyColorNumber, typableMapId);
            }
        }

        void Session_PreSendUpdateStatus(object sender, StatusUpdateEventArgs e)
        {
            // Typable Map コマンド?
            if (Session.Config.EnableTypableMap)
            {
                if (_typableMapCommands.Process(e.ReceivedMessage))
                {
                    e.Cancel = true;
                    return;
                }
            }
        }

        void Session_ConfigChanged(object sender, EventArgs e)
        {
            if (Session.Config.EnableTypableMap)
            {
                if (_typableMapCommands == null)
                    _typableMapCommands = new TypableMapCommandProcessor(Session.TwitterService, Session, Session.Config.TypableMapKeySize);
                if (_typableMapCommands.TypableMapKeySize != Session.Config.TypableMapKeySize)
                    _typableMapCommands.TypableMapKeySize = Session.Config.TypableMapKeySize;
            }
            else
            {
                _typableMapCommands = null;
            }
        }
    }
}
