using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Text;
using Misuzilla.Net.Irc;

namespace Misuzilla.Applications.TwitterIrcGateway.AddIns.Console
{
    /// <summary>
    /// 
    /// </summary>
    [Description("グループの設定を行うコンテキストに切り替えます")]
    public class GroupContext : Context
    {
        [Description("指定したグループ(チャンネル)にユーザを追加します")]
        public void Invite(String[] channelNameAndUserNames)
        {
            if (channelNameAndUserNames.Length == 1)
            {
                ConsoleAddIn.NotifyMessage("エラー: ユーザが指定されていません。");
                return;
            }

            if (!Session.Groups.ContainsKey(channelNameAndUserNames[0]))
            {
                ConsoleAddIn.NotifyMessage("エラー: 指定されたグループは存在しません。");
                return;
            }

            for (var i = 1; i < channelNameAndUserNames.Length; i++)
            {
                Group group = Session.Groups[channelNameAndUserNames[0]];
                String userName = channelNameAndUserNames[i];
                if (!group.Exists(userName) && (String.Compare(userName, Session.Nick, true) != 0))
                {
                    group.Add(userName);
                    if (group.IsJoined)
                    {
                        JoinMessage joinMsg = new JoinMessage(channelNameAndUserNames[0], "")
                        {
                            SenderHost = "twitter@" + Server.ServerName,
                            SenderNick = userName
                        };
                        Session.Send(joinMsg);
                    }
                }

            }

            Session.SaveGroups();
        }
    }
}
