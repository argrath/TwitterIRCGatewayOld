using System.ComponentModel;

namespace Misuzilla.Applications.TwitterIrcGateway.AddIns.Console
{
    /// <summary>
    /// 
    /// </summary>
    [Description("設定を行うコンテキストに切り替えます")]
    public class ConfigContext : Context
    {
        public override IConfiguration[] Configurations { get { return new IConfiguration[] { Console.Config, CurrentSession.Config }; } }
        protected override void OnConfigurationChanged(IConfiguration config, System.Reflection.MemberInfo memberInfo, object value)
        {
            if (config is GeneralConfig)
            {
                CurrentSession.AddInManager.SaveConfig(Console.Config);
            }
            else if (config is Config)
            {
                CurrentSession.SaveConfig();
                CurrentSession.OnConfigChanged();

                if (memberInfo.Name == "BufferSize")
                    CurrentSession.TwitterService.BufferSize = CurrentSession.Config.BufferSize;

                // 取得間隔またはチェックの必要性が変更になったらタイマーを再起動する
                if (memberInfo.Name.StartsWith("Interval") || memberInfo.Name == "EnableRepliesCheck" || memberInfo.Name == "IntervalReplies" || memberInfo.Name == "IntervalDirectMessage")
                {
                    CurrentSession.TwitterService.Interval = CurrentSession.Config.Interval;
                    CurrentSession.TwitterService.IntervalReplies = CurrentSession.Config.IntervalReplies;
                    CurrentSession.TwitterService.IntervalDirectMessage = CurrentSession.Config.IntervalDirectMessage;
                    CurrentSession.TwitterService.Stop();
                    CurrentSession.TwitterService.Start();
                }
            }
        }
    }
}
