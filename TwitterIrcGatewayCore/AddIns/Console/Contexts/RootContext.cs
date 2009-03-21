﻿using System;
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
}