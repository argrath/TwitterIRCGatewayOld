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

        [Description("指定したグループ(チャンネル)からユーザを削除します")]
        public void Kick(String[] channelNameAndUserNames)
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
                if (group.Exists(userName))
                {
                    group.Remove(userName);
                    if (group.IsJoined)
                    {
                        PartMessage partMsg = new PartMessage(channelNameAndUserNames[0], "")
                        {
                            SenderHost = "twitter@" + Server.ServerName,
                            SenderNick = userName
                        };
                        CurrentSession.Send(partMsg);
                    }
                }

            }

            CurrentSession.SaveGroups();
        }
    
        [Description("")]
        public void Rename(String oldChannelName, String newChannelName)
        {
            if (CurrentSession.Groups.ContainsKey(oldChannelName))
            {
                Console.NotifyMessage("既に存在するチャンネル名を指定することは出来ません。");
            }
            // 旧メインチャンネルをPART
            CurrentSession.SendServer(new PartMessage(oldChannelName, ""));

            Group g = CurrentSession.Groups[oldChannelName];
            g.Name = newChannelName;
            CurrentSession.Groups.Remove(oldChannelName);
            CurrentSession.Groups.Add(newChannelName, g);
            
            // 新メインチャンネルにJOIN
            CurrentSession.SendServer(new JoinMessage(newChannelName, ""));
        }
    }
}
