using System;
using System.ComponentModel;
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
                Console.NotifyMessage("エラー: ユーザが指定されていません。");
                return;
            }

            if (!Session.Groups.ContainsKey(channelNameAndUserNames[0]))
            {
                Console.NotifyMessage("エラー: 指定されたグループは存在しません。");
                return;
            }

            for (var i = 1; i < channelNameAndUserNames.Length; i++)
            {
                Group group = CurrentSession.Groups[channelNameAndUserNames[0]];
                String userName = channelNameAndUserNames[i];
                if (!group.Exists(userName) && (String.Compare(userName, CurrentSession.Nick, true) != 0))
                {
                    group.Add(userName);
                    if (group.IsJoined)
                    {
                        JoinMessage joinMsg = new JoinMessage(channelNameAndUserNames[0], "")
                        {
                            SenderHost = "twitter@" + Server.ServerName,
                            SenderNick = userName
                        };
                        CurrentSession.Send(joinMsg);
                    }
                }

            }

            CurrentSession.SaveGroups();
        }
    }
}
