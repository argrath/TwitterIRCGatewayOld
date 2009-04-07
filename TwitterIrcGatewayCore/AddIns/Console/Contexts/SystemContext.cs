using System;
using System.ComponentModel;
using System.IO;
using System.Reflection;

namespace Misuzilla.Applications.TwitterIrcGateway.AddIns.Console
{
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
            foreach (var t in Session.AddInManager.AddInTypes)
            {
                if ((String.Compare(t.FullName, addInName, true) == 0) && typeof(IAddIn).IsAssignableFrom(t) &&
                    !t.IsAbstract && t.IsClass)
                {
                    if (!Session.Config.DisabledAddInsList.Contains(t.FullName))
                    {
                        Session.Config.DisabledAddInsList.Add(t.FullName);
                        Session.SaveConfig();
                    }
                    ConsoleAddIn.NotifyMessage(String.Format("アドイン \"{0}\" は無効化されました。次回接続時まで設定は反映されません。", t.FullName));
                    return;
                }
            }

            ConsoleAddIn.NotifyMessage(String.Format("アドイン \"{0}\" は読み込まれていません。", addInName));
        }

        [Description("アドインを有効にします")]
        public void EnableAddIn(String addInName)
        {
            if (String.IsNullOrEmpty(addInName))
            {
                ConsoleAddIn.NotifyMessage("アドインの名前を指定する必要があります。");
                return;
            }

            foreach (var t in Session.AddInManager.AddInTypes)
            {
                if ((String.Compare(t.FullName, addInName, true) == 0) && typeof(IAddIn).IsAssignableFrom(t) && !t.IsAbstract && t.IsClass)
                {
                    if (Session.Config.DisabledAddInsList.Contains(t.FullName))
                    {
                        Session.Config.DisabledAddInsList.Remove(t.FullName);
                        Session.SaveConfig();
                    }
                    ConsoleAddIn.NotifyMessage(String.Format("アドイン \"{0}\" は有効化されました。次回接続時まで設定は反映されません。", t.FullName));
                    return;
                }
            }
            ConsoleAddIn.NotifyMessage(String.Format("アドイン \"{0}\" は読み込まれていません。", addInName));
        }

        [Description("アドインを再読込します")]
        public void ReloadAddIns()
        {
            Session.AddInManager.RestartAddIns();
        }

        [Description("バージョン情報を表示します")]
        public void Version()
        {
            Assembly asm = typeof(Server).Assembly;
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
