using System;
using System.Collections.Generic;
using System.Text;
using System.Net;
using System.Text.RegularExpressions;
using System.Diagnostics;
using Misuzilla.Applications.TwitterIrcGateway.AddIns.Console;
using System.ComponentModel;
using System.Xml.Serialization;

namespace Misuzilla.Applications.TwitterIrcGateway.AddIns.SocialRemoveRedundantSuffix
{
    [Description("ネットワーク経由で削除すべき末尾文字列のリストを取得します")]
    public class SocialRemoveRedundantSuffixAddIn : AddInBase
    {
        private Regex _regex;
        private String _blackList;

        public override void Initialize()
        {
            try
            {
                Configuration config = Session.AddInManager.GetConfig<Configuration>();
                Session.AddInManager.SaveConfig(config);
                UpdateList();
            }
            catch (WebException ex)
            {
                Trace.WriteLine("Download Failed: " + ex.Message);
            }
            Session.PostFilterProcessTimelineStatus += new EventHandler<TimelineStatusEventArgs>(Session_PostFilterProcessTimelineStatus);
            Session.AddInsLoadCompleted += (sender, e) => Session.AddInManager.GetAddIn<ConsoleAddIn>().RegisterContext<SocialRemoveRedundantSuffixContext>();
        }

        public void UpdateList()
        {
            try
            {
                using (WebClient webClient = new WebClient())
                {
                    Configuration config = Session.AddInManager.GetConfig<Configuration>();
                    String url = config.BlackListUrl;
                    Trace.WriteLine("Download Blacklist: " + url);
                    _blackList = webClient.DownloadString(url);
                    config.BlackListCache = _blackList;
                    Session.AddInManager.SaveConfig(config);
                }
            }
            finally
            {
                BuildRegex();
            }
        }

        private void BuildRegex()
        {
            if (_blackList == null)
            {
                _regex = null;
            }
            else
            {
                String[] lines = Regex.Escape(_blackList.Replace("\r", "")).Split(new string[] { "\\n" }, StringSplitOptions.RemoveEmptyEntries);
                Trace.WriteLine("Black list: " + lines.Length + " lines");
                if (lines.Length == 0)
                {
                    _regex = null;
                }
                else
                {
                    _regex = new Regex("\\s*(" + String.Join("|", lines) + ")\\s*$", RegexOptions.CultureInvariant);
                }    
            }
        }

        private void Session_PostFilterProcessTimelineStatus(Object sender, TimelineStatusEventArgs e)
        {
            if (_regex != null)
            {
                e.Text = _regex.Replace(e.Text, "");
            }
        }
    }

    [Description("冗長な末尾文字列削除機能のコンテキストに切り替えます")]
    public class SocialRemoveRedundantSuffixContext : Context
    {
        [Description("ブラックリストを更新します")]
        public void Update()
        {
            Configuration config = Session.AddInManager.GetConfig<Configuration>();
            ConsoleAddIn.NotifyMessage("ブラックリストを " + config.BlackListUrl + " から取得しています。");
            try
            {
                Session.AddInManager.GetAddIn<SocialRemoveRedundantSuffixAddIn>().UpdateList();
                ConsoleAddIn.NotifyMessage("ブラックリストを更新しました。");
            }
            catch (WebException ex)
            {
                ConsoleAddIn.NotifyMessage("ブラックリストの更新時にエラーが発生しました。: "+ex.Message);
            }
        }

        [Description("ブラックリストのURLを設定します")]
        public void BlackListUrl(String url)
        {
            Configuration config = Session.AddInManager.GetConfig<Configuration>();
            if (!String.IsNullOrEmpty(url))
                config.BlackListUrl = url;
            ConsoleAddIn.NotifyMessage("BlackListUrl = " + config.BlackListUrl);
            Session.AddInManager.SaveConfig(config);
        }
    }
}
