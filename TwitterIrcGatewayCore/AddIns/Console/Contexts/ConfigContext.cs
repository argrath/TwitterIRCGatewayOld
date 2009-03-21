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
            }
        }
    }
}
