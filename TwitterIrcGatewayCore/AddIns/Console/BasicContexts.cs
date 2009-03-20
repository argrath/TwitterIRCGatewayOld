using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml;
using Misuzilla.Applications.TwitterIrcGateway.Filter;
using Misuzilla.Net.Irc;
using System.Reflection;

namespace Misuzilla.Applications.TwitterIrcGateway.AddIns.Console
{
    /// <summary>
    /// 
    /// </summary>
    [Browsable(false)]
    public class RootContext : Context
    {
        public override Type[] Contexts { get { return ConsoleAddIn.Contexts.ToArray(); } }

        [Description("Twitter 検索を利用して検索します")]
        public void Search(String keywords)
        {
            try
            {
                XmlDocument xmlDoc = new XmlDocument();
                xmlDoc.Load("http://pcod.no-ip.org/yats/search?rss&query=" + Utility.UrlEncode(keywords));
                XmlNamespaceManager nsMgr = new XmlNamespaceManager(xmlDoc.NameTable);
                nsMgr.AddNamespace("a", "http://www.w3.org/2005/Atom");

                XmlNodeList entries = xmlDoc.SelectNodes("//a:entry", nsMgr);

                if (entries.Count == 0)
                {
                    ConsoleAddIn.NotifyMessage("検索結果は見つかりませんでした。");
                    return;
                }

                for (var i = (Math.Min(entries.Count, ConsoleAddIn.Config.SearchCount)); i > 0; i--)
                {
                    // 後ろから取っていく
                    XmlElement entryE = entries[i - 1] as XmlElement;
                    String screenName = entryE.SelectSingleNode("a:title/text()", nsMgr).Value;
                    String body = entryE.SelectSingleNode("a:summary/text()", nsMgr).Value;
                    String link = entryE.SelectSingleNode("a:link/@href", nsMgr).Value;
                    DateTime updated = DateTime.Parse(entryE.SelectSingleNode("a:updated/text()", nsMgr).Value);

                    body = Regex.Replace(body, "^@[^ ]+ : ", "");

                    StringBuilder sb = new StringBuilder();
                    sb.Append(updated.ToString("HH:mm")).Append(": ").Append(body);
                    if (ConsoleAddIn.Config.ShowPermalinkAfterStatus)
                        sb.Append(" ").Append(link);

                    ConsoleAddIn.NotifyMessage(screenName, sb.ToString());
                }
            }
            catch (WebException we)
            {
                ConsoleAddIn.NotifyMessage("Twitter 検索へのリクエスト中にエラーが発生しました:");
                ConsoleAddIn.NotifyMessage(we.Message);
            }
        }

        [Description("指定したユーザのタイムラインを取得します")]
        public void Timeline(params String[] screenNames)
        {
            List<Status> statuses = new List<Status>();
            foreach (var screenName in screenNames)
            {
                try
                {
                    var retStatuses = Session.TwitterService.GetTimelineByScreenName(screenName, new DateTime(), ConsoleAddIn.Config.SearchCount);
                    statuses.AddRange(retStatuses.Status);
                }
                catch (TwitterServiceException te)
                {
                    ConsoleAddIn.NotifyMessage(String.Format("ユーザ {0} のタイムラインを取得中にエラーが発生しました:", screenName));
                    ConsoleAddIn.NotifyMessage(te.Message);
                }
                catch (WebException we)
                {
                    ConsoleAddIn.NotifyMessage(String.Format("ユーザ {0} のタイムラインを取得中にエラーが発生しました:", screenName));
                    ConsoleAddIn.NotifyMessage(we.Message);
                }
            }

            ShowStatuses(statuses);
        }

        [Description("指定したユーザの Favorites を取得します")]
        public void Favorites(params String[] screenNames)
        {
            List<Status> statuses = new List<Status>();
            foreach (var screenName in screenNames)
            {
                try
                {
                    var retStatuses = Session.TwitterService.GetFavoritesByScreenName(screenName, 1);
                    statuses.AddRange(retStatuses.Status);
                    if (statuses.Count > ConsoleAddIn.Config.FavoritesCount)
                        statuses.RemoveRange(10, statuses.Count - 10);
                }
                catch (TwitterServiceException te)
                {
                    ConsoleAddIn.NotifyMessage(String.Format("ユーザ {0} の Favorites を取得中にエラーが発生しました:", screenName));
                    ConsoleAddIn.NotifyMessage(te.Message);
                }
                catch (WebException we)
                {
                    ConsoleAddIn.NotifyMessage(String.Format("ユーザ {0} の Favorites を取得中にエラーが発生しました:", screenName));
                    ConsoleAddIn.NotifyMessage(we.Message);
                }
            }

            ShowStatuses(statuses);
        }

        [Description("指定したユーザを follow します")]
        public void Follow(params String[] screenNames)
        {
            FollowOrRemove(true, screenNames);
        }

        [Description("指定したユーザを remove します")]
        public void Remove(params String[] screenNames)
        {
            FollowOrRemove(false, screenNames);
        }

        //
        [Browsable(false)]
        private void FollowOrRemove(Boolean follow, String[] screenNames)
        {
            String action = follow ? "follow" : "remove";

            foreach (var screenName in screenNames)
            {
                try
                {
                    var user = follow
                                   ? Session.TwitterService.CreateFriendship(screenName)
                                   : Session.TwitterService.DestroyFriendship(screenName);
                    ConsoleAddIn.NotifyMessage(String.Format("ユーザ {0} を {1} しました。", screenName, action));
                }
                catch (TwitterServiceException te)
                {
                    ConsoleAddIn.NotifyMessage(String.Format("ユーザ {0} を {1} する際にエラーが発生しました:", screenName, action));
                    ConsoleAddIn.NotifyMessage(te.Message);
                }
                catch (WebException we)
                {
                    ConsoleAddIn.NotifyMessage(String.Format("ユーザ {0} を {1} する際にエラーが発生しました:", screenName, action));
                    ConsoleAddIn.NotifyMessage(we.Message);
                }
            }
        }

        private void ShowStatuses(List<Status> statuses)
        {
            statuses.Sort((a, b) => ((a.Id == b.Id) ? 0 : ((a.Id > b.Id) ? 1 : -1)));
            foreach (var status in statuses)
            {
                StringBuilder sb = new StringBuilder();
                sb.AppendFormat("{0}: {1}", status.CreatedAt.ToString("HH:mm"), status.Text);
                if (ConsoleAddIn.Config.ShowPermalinkAfterStatus)
                    sb.AppendFormat(" http://twitter.com/{0}/status/{1}", status.User.ScreenName, status.Id);

                ConsoleAddIn.NotifyMessage(status.User.ScreenName, sb.ToString());
            }
        }
    }

    /// <summary>
    /// 
    /// </summary>
    [Description("フィルタの設定を行うコンテキストに切り替えます")]
    public class FilterContext : Context
    {
        [Description("存在するフィルタをすべて表示します")]
        public void List()
        {
            for (var i = 0; i < Session.Filters.Items.Length; i++)
            {
                FilterItem filter = Session.Filters.Items[i];
                ConsoleAddIn.NotifyMessage(String.Format("{0}: {1}", i, filter.ToString()));
            }
        }

        [Description("指定したフィルタを有効化します")]
        public void Enable(String args)
        {
            SwitchEnable(args, true);
        }

        [Description("指定したフィルタを無効化します")]
        public void Disable(String args)
        {
            SwitchEnable(args, false);
        }

        private void SwitchEnable(String args, Boolean enable)
        {
            Int32 index;
            FilterItem[] items = Session.Filters.Items;
            if (Int32.TryParse(args, out index))
            {
                if (index < items.Length && index > -1)
                {
                    items[index].Enabled = enable;
                    Session.SaveFilters();
                    ConsoleAddIn.NotifyMessage(String.Format("フィルタ {0} を{1}化しました。", items[index], (enable ? "有効" : "無効")));
                }
                else
                {
                    ConsoleAddIn.NotifyMessage("存在しないフィルタが指定されました。");
                }
            }
            else
            {
                ConsoleAddIn.NotifyMessage("フィルタの指定が正しくありません。");
            }
        }
    }

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

    /// <summary>
    /// 
    /// </summary>
    [Description("設定を行うコンテキストに切り替えます")]
    public class ConfigContext : Context
    {
        [Description("Search コマンドでの検索時の表示件数を指定します")]
        public void SearchCount(Int32 count)
        {
            ConsoleAddIn.Config.SearchCount = count;
            ConsoleAddIn.NotifyMessage("SearchCount = " + count);
            Session.AddInManager.SaveConfig(ConsoleAddIn.Config);
        }

        [Description("Timeline コマンドでのタイムライン取得時の各タイムラインごとの表示件数を指定します")]
        public void TimelineCount(Int32 count)
        {
            ConsoleAddIn.Config.TimelineCount = count;
            ConsoleAddIn.NotifyMessage("TimelineCount = " + count);
            Session.AddInManager.SaveConfig(ConsoleAddIn.Config);
        }

        [Description("Timeline, Search, Favorites コマンドでのステータスの後ろにURLをつけるかどうかを指定します")]
        public void ShowPermalinkAfterStatus(Boolean value)
        {
            ConsoleAddIn.Config.ShowPermalinkAfterStatus = value;
            ConsoleAddIn.NotifyMessage("ShowPermalinkAfterStatus = " + value);
            Session.AddInManager.SaveConfig(ConsoleAddIn.Config);
        }

        [Description("Favorites コマンドでのステータス取得時の表示件数を指定します")]
        public void FavoritesCount(Int32 value)
        {
            ConsoleAddIn.Config.FavoritesCount = value;
            ConsoleAddIn.NotifyMessage("FavoritesCount = " + value);
            Session.AddInManager.SaveConfig(ConsoleAddIn.Config);
        }
    }

    [Description("システムに関連するコンテキストに切り替えます")]
    public class SystemContext : Context
    {
        [Description("アドインの一覧を表示します")]
        public void ShowAddIns()
        {
            foreach (Type addInType in Session.AddInManager.AddInTypes)
            {
                Assembly addinAsm = addInType.Assembly;
                if (addinAsm == Assembly.GetExecutingAssembly())
                    continue;
                
                ConsoleAddIn.NotifyMessage(String.Format("{0} {1} {2}",
                                                         addInType.FullName,
                                                         addinAsm.GetName().Version,
                                                         (Session.AddInManager.GetAddIn(addInType) == null
                                                              ? "(Disabled)"
                                                              : "")
                                               ));
            }
        }

        [Description("アドインを無効にします")]
        public void DisableAddIn(String addInName)
        {
            if (String.IsNullOrEmpty(addInName))
            {
                ConsoleAddIn.NotifyMessage("アドインの名前を指定する必要があります。");
                return;
            }

            try
            {
                Type t = Type.GetType(addInName);
                if (typeof(IAddIn).IsAssignableFrom(t) && !t.IsAbstract && t.IsClass)
                {
                    if (!Session.Config.DisabledAddInsList.Contains(t.FullName))
                    {
                        Session.Config.DisabledAddInsList.Add(t.FullName);
                        Session.SaveConfig();
                        ConsoleAddIn.NotifyMessage(String.Format("アドイン \"{0}\" は無効化されました。次回接続時まで設定は反映されません。", t.FullName));
                    }
                }
            }
            catch (Exception e)
            {
                ConsoleAddIn.NotifyMessage(String.Format("アドイン \"{0}\" は読み込まれていません。", addInName));
            }
        }

        [Description("アドインを有効にします")]
        public void EnableAddIn(String addInName)
        {
            if (String.IsNullOrEmpty(addInName))
            {
                ConsoleAddIn.NotifyMessage("アドインの名前を指定する必要があります。");
                return;
            }

            Type t = Type.GetType(addInName, false, true);
            if (t != null)
            {
                if (typeof(IAddIn).IsAssignableFrom(t) && !t.IsAbstract && t.IsClass)
                {
                    if (Session.Config.DisabledAddInsList.Contains(t.FullName))
                    {
                        Session.Config.DisabledAddInsList.Remove(t.FullName);
                        Session.SaveConfig();
                        ConsoleAddIn.NotifyMessage(String.Format("アドイン \"{0}\" は有効化されました。次回接続時まで設定は反映されません。", t.FullName));
                    }
                }
            }
            else
            {
                ConsoleAddIn.NotifyMessage(String.Format("アドイン \"{0}\" は読み込まれていません。", addInName));
            }
        }

        [Description("アドインを再読込します")]
        public void ReloadAddIns()
        {
            Session.AddInManager.RestartAddIns();
        }

        [Description("バージョン情報を表示します")]
        public void Version()
        {
            Assembly asm = typeof (Server).Assembly;
            AssemblyName asmName = asm.GetName();
            ConsoleAddIn.NotifyMessage(String.Format("TwitterIrcGateway {0}", asmName.Version));
        }

        [Description("システム情報を表示します")]
        public void ShowInfo()
        {
            Assembly asm = typeof(Server).Assembly;
            AssemblyName asmName = asm.GetName();

            ConsoleAddIn.NotifyMessage("[Core]");
            ConsoleAddIn.NotifyMessage(String.Format("TwitterIrcGateway {0}", asmName.Version));
            ConsoleAddIn.NotifyMessage(String.Format("Location: {0}", asm.Location));
            ConsoleAddIn.NotifyMessage(String.Format("BaseDirectory: {0}", Path.GetDirectoryName(Assembly.GetEntryAssembly().Location)));

            ConsoleAddIn.NotifyMessage("[System]");
            ConsoleAddIn.NotifyMessage(String.Format("Operating System: {0}", Environment.OSVersion));
            ConsoleAddIn.NotifyMessage(String.Format("Runtime Version: {0}", Environment.Version));

            ConsoleAddIn.NotifyMessage("[Session]");
            ConsoleAddIn.NotifyMessage(String.Format("ConfigDirectory: {0}", Session.UserConfigDirectory));
            if (Session.TwitterUser != null)
            {
                ConsoleAddIn.NotifyMessage(String.Format("TwitterUser: {0} ({1})", Session.TwitterUser.ScreenName,
                                                         Session.TwitterUser.Id));
            }

            ConsoleAddIn.NotifyMessage("[AddIns]");
            foreach (IAddIn addIn in Session.AddInManager.AddIns)
            {
                Assembly addinAsm = addIn.GetType().Assembly;
                if (addinAsm != asm)
                {
                    ConsoleAddIn.NotifyMessage(String.Format("{0} {1}", addIn.GetType().FullName,
                                                             addinAsm.GetName().Version));
                }
            }
        }
    }
}
