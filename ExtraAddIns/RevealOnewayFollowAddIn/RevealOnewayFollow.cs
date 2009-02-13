using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Text;
using System.Xml;
using Misuzilla.Applications.TwitterIrcGateway.AddIns.Console;

namespace Misuzilla.Applications.TwitterIrcGateway.AddIns
{
    [Description("片思いチェックの設定を行うコンテキストに切り替えます")]
    public class RevealOnewayFollowContext : Context
    {
        [Description("片思いチェックで利用しているFollowされているユーザーのリストを更新します。")]
        public void UpdateFollowerIds()
        {
            RevealOnewayFollow addIn = Session.AddInManager.GetAddIn<RevealOnewayFollow>();
            ConsoleAddIn.NotifyMessage("Follower リストを更新しています。");
            addIn.UpdateFollowerIds();
            ConsoleAddIn.NotifyMessage("Follower リストを更新しました。現在、" + addIn.FollowerIds.Count.ToString() + "人のユーザーに Follow されています。");
        }
    }
    public class RevealOnewayFollow : AddInBase
    {
        private List<Int32> _followerIds;
        internal List<Int32> FollowerIds { get { return _followerIds; } }

        public override void Initialize()
        {
            Session.AddInsLoadCompleted += (sender, e) =>
                                               {
                                                   Session.AddInManager.GetAddIn<ConsoleAddIn>().RegisterContext<RevealOnewayFollowContext>();
                                                   Session.PreSendMessageTimelineStatus += new EventHandler<TimelineStatusEventArgs>(Session_PreSendMessageTimelineStatus);
                                               };
        }

        void Session_PreSendMessageTimelineStatus(object sender, TimelineStatusEventArgs e)
        {
            if (_followerIds != null || UpdateFollowerIds())
            {
                if (e.Status.User.Id != Session.TwitterUser.Id && _followerIds.BinarySearch(e.Status.User.Id) < 0)
                {
                    e.Text += " (片思い)";
                }
            }
        }

        internal Boolean UpdateFollowerIds()
        {
             if (Session.TwitterUser == null)
                 return false;

            return Session.RunCheck(() =>
                                 {
                                     try
                                     {
                                         String idsXml =
                                             Session.TwitterService.GET("/followers/ids/" + Session.TwitterUser.Id +
                                                                        ".xml");
                                         XmlDocument xmlDoc = new XmlDocument();
                                         xmlDoc.LoadXml(idsXml);
                                         List<Int32> followerIds = new List<Int32>();
                                         foreach (XmlElement E in xmlDoc.GetElementsByTagName("id"))
                                         {
                                             followerIds.Add(Int32.Parse(E.InnerText));
                                         }
                                         followerIds.Sort();
                                         _followerIds = followerIds;
                                         Trace.WriteLine("Followers: "+_followerIds.Count.ToString());
                                     }
                                     catch (XmlException ex)
                                     {
                                         Session.SendTwitterGatewayServerMessage("エラー: Follower リストを取得時にエラーが発生しました。("+ex.Message+")");
                                     }
                                 });
        }
    }
}
