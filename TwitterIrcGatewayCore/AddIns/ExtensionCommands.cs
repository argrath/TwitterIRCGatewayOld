using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Reflection;
using System.Text;

namespace Misuzilla.Applications.TwitterIrcGateway.AddIns
{
    class ExtensionCommands : AddInBase
    {
        public override void Initialize()
        {
            Session.MessageReceived += new EventHandler<MessageReceivedEventArgs>(MessageReceived_TIGGC);
            Session.MessageReceived += new EventHandler<MessageReceivedEventArgs>(MessageReceived_TIGCONFIG);
            Session.MessageReceived += new EventHandler<MessageReceivedEventArgs>(MessageReceived_TIGLOADFILTER);
        }

        void MessageReceived_TIGGC(object sender, MessageReceivedEventArgs e)
        {
            if (String.Compare(e.Message.Command, "TIGGC", true) != 0) return;
            Int64 memUsage = GC.GetTotalMemory(false);
            GC.Collect();
            Session.SendTwitterGatewayServerMessage(String.Format("Garbage Collect: {0:###,##0} bytes -> {1:###,##0} bytes", memUsage, GC.GetTotalMemory(false)));
        }

        void MessageReceived_TIGLOADFILTER(object sender, MessageReceivedEventArgs e)
        {
            if (String.Compare(e.Message.Command, "TIGLOADFILTER", true) != 0) return;
            Session.LoadFilters();
        }

        void MessageReceived_TIGCONFIG(object sender, MessageReceivedEventArgs e)
        {
            if (String.Compare(e.Message.Command, "TIGCONFIG", true) != 0) return;

            Type t = typeof(Config);

            // プロパティ一覧を作る
            if (String.IsNullOrEmpty(e.Message.CommandParams[0]))
            {
                //SendTwitterGatewayServerMessage("TIGCONFIG コマンドは1つまたは2つの引数(ConfigName, Value)が必要です。");
                foreach (var pi in t.GetProperties(BindingFlags.Public | BindingFlags.IgnoreCase | BindingFlags.Instance | BindingFlags.SetProperty))
                {
                    Session.SendTwitterGatewayServerMessage(
                        String.Format("{0} ({1}) = {2}", pi.Name, pi.PropertyType.FullName, pi.GetValue(Session.Config, null)));
                }
                return;
            }

            // プロパティを探す
            String propName = e.Message.CommandParams[0];
            PropertyInfo propInfo = t.GetProperty(propName, BindingFlags.Public | BindingFlags.IgnoreCase | BindingFlags.Instance | BindingFlags.SetProperty);
            if (propInfo == null)
            {
                Session.SendTwitterGatewayServerMessage(String.Format("設定項目 \"{0}\" は存在しません。", propName));
                return;
            }

            // 2つめの引数があるときは値を設定する。
            if (!String.IsNullOrEmpty(e.Message.CommandParams[1]))
            {
                TypeConverter tConv = TypeDescriptor.GetConverter(propInfo.PropertyType);
                if (!tConv.CanConvertFrom(typeof(String)))
                {
                    Session.SendTwitterGatewayServerMessage(
                        String.Format("設定項目 \"{0}\" の型 \"{1}\" には適切な TypeConverter がないため、このコマンドで設定することはできません。", propName,
                                      propInfo.PropertyType.FullName));
                    return;
                }

                try
                {
                    Object value = tConv.ConvertFromString(e.Message.CommandParams[1]);
                    propInfo.SetValue(Session.Config, value, null);
                }
                catch (Exception ex)
                {
                    Session.SendTwitterGatewayServerMessage(String.Format(
                                                        "設定項目 \"{0}\" の型 \"{1}\" に値を変換し設定する際にエラーが発生しました({2})。", propName,
                                                        propInfo.PropertyType.FullName, ex.GetType().Name));
                    foreach (var line in ex.Message.Split('\n'))
                        Session.SendTwitterGatewayServerMessage(line);
                }

                Session.SaveConfig();
            }

            Session.SendTwitterGatewayServerMessage(
                String.Format("{0} ({1}) = {2}", propName, propInfo.PropertyType.FullName, propInfo.GetValue(Session.Config, null)));
        }
    }
}
