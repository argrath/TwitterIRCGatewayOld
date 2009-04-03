using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Text;

namespace Misuzilla.Applications.TwitterIrcGateway.AddIns.Console
{
    /// <summary>
    /// 
    /// </summary>
    [Description("設定を行うコンテキストに切り替えます")]
    public class ConfigContext : Context
    {
        public override IConfiguration[] Configurations { get { return new IConfiguration[] { ConsoleAddIn.Config, Session.Config }; } }
        protected override void OnConfigurationChanged(IConfiguration config, System.Reflection.MemberInfo memberInfo, object value)
        {
            if (config is GeneralConfig)
            {
                Session.AddInManager.SaveConfig(ConsoleAddIn.Config);
            }
            else if (config is Config)
            {
                Session.SaveConfig();
                Session.OnConfigChanged();

                // 取得間隔またはチェックの必要性が変更になったらタイマーを再起動する
                if (memberInfo.Name.StartsWith("Interval") || memberInfo.Name == "EnableRepliesCheck")
                {
                    Session.TwitterService.Interval = Session.Config.Interval;
                    Session.TwitterService.IntervalReplies = Session.Config.IntervalReplies;
                    Session.TwitterService.IntervalDirectMessage = Session.Config.IntervalDirectMessage;
                    Session.TwitterService.Stop();
                    Session.TwitterService.Start();
                }
            }
        }
    }
}
