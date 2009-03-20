using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Reflection;
using Misuzilla.Net.Irc;

namespace Misuzilla.Applications.TwitterIrcGateway.AddIns.Console
{
    /// <summary>
    /// 
    /// </summary>
    public abstract class Context : IDisposable
    {
        [Browsable(false)]
        public Session Session { get; set; }
        [Browsable(false)]
        public Server Server { get; set; }
        [Browsable(false)]
        public ConsoleAddIn ConsoleAddIn { get { return Session.AddInManager.GetAddIn<Console.ConsoleAddIn>(); } }

        public virtual Type[] Contexts { get { return new Type[0]; } }
        
        /// <summary>
        /// 
        /// </summary>
        [Browsable(false)]
        public virtual void Initialize()
        {
        }

        [Description("コマンドの一覧または説明を表示します")]
        public void Help(String commandName)
        {
            if (String.IsNullOrEmpty(commandName))
            {
                // コマンドの一覧
                if (this.Contexts.Length > 0)
                {
                    ConsoleAddIn.NotifyMessage("[Contexts]");
                    foreach (var ctx in this.Contexts)
                    {
                        if (IsBrowsable(ctx))
                            ConsoleAddIn.NotifyMessage(String.Format("{0} - {1}", ctx.Name.Replace("Context", ""),
                                                                     GetDescription(ctx)));
                    }
                }

                ConsoleAddIn.NotifyMessage("[Commands]");
                MethodInfo[] methodInfoArr = this.GetType().GetMethods(BindingFlags.Instance | BindingFlags.Public);
                Type t = typeof(Context);
                foreach (var methodInfo in methodInfoArr)
                {
                    if (t.IsAssignableFrom(methodInfo.DeclaringType) && !methodInfo.IsConstructor && !methodInfo.IsFinal &&
                        !methodInfo.IsSpecialName)
                    {
                        if (IsBrowsable(methodInfo))
                            ConsoleAddIn.NotifyMessage(String.Format("{0} - {1}", methodInfo.Name,
                                                                     GetDescription(methodInfo)));
                    }
                }
            }
            else
            {
                // コマンドの説明
                MethodInfo methodInfo = GetCommand(commandName);
                if (methodInfo == null)
                {
                    ConsoleAddIn.NotifyMessage("指定された名前はこのコンテキストのコマンドに見つかりません。");
                    return;
                }

                String desc = GetDescription(methodInfo);
                if (!String.IsNullOrEmpty(desc))
                    ConsoleAddIn.NotifyMessage(desc);
                
                ParameterInfo[] paramInfo = methodInfo.GetParameters();
                if (paramInfo.Length > 0)
                {
                    ConsoleAddIn.NotifyMessage("引数:");
                    foreach (var paramInfoItem in paramInfo)
                    {
                        desc = GetDescription(paramInfoItem);
                        ConsoleAddIn.NotifyMessage(String.Format("- {0}: {1}",
                                                                 (String.IsNullOrEmpty(desc) ? paramInfoItem.Name : desc),
                                                                 paramInfoItem.ParameterType));
                    }
                }
            }
        }

        [Description("コンテキストを一つ前のものに戻します")]
        public void Exit()
        {
            ConsoleAddIn.PopContext();
        }

        #region Context Helpers

        /// <summary>
        /// コマンドを名前で取得します。
        /// </summary>
        /// <param name="commandName"></param>
        [Browsable(false)]
        public MethodInfo GetCommand(String commandName)
        {
            MethodInfo methodInfo = this.GetType().GetMethod(commandName,
                                                                 BindingFlags.Instance | BindingFlags.Public |
                                                                 BindingFlags.IgnoreCase);

            if (methodInfo != null &&(methodInfo.IsFinal || methodInfo.IsConstructor || methodInfo.IsSpecialName || IsBrowsable(methodInfo)))
            {
                return methodInfo;
            }

            return null;
        }
        
        private Boolean IsBrowsable(Type t)
        {
            Object[] attrs = t.GetCustomAttributes(typeof(BrowsableAttribute), true);
            return !(attrs.Length != 0 && !((BrowsableAttribute)attrs[0]).Browsable);
        }
        private Boolean IsBrowsable(ICustomAttributeProvider customAttributeProvider)
        {
            Object[] attrs = customAttributeProvider.GetCustomAttributes(typeof(BrowsableAttribute), true);
            return !(attrs.Length != 0 && !((BrowsableAttribute)attrs[0]).Browsable);
        }
        private String GetDescription(Type t)
        {
            Object[] attrs = t.GetCustomAttributes(typeof(DescriptionAttribute), true);
            return (attrs.Length == 0) ? "" : ((DescriptionAttribute)attrs[0]).Description;
        }
        private String GetDescription(ICustomAttributeProvider customAttributeProvider)
        {
            Object[] attrs = customAttributeProvider.GetCustomAttributes(typeof(DescriptionAttribute), true);
            return (attrs.Length == 0) ? "" : ((DescriptionAttribute)attrs[0]).Description;
        }
        #endregion

        #region IDisposable メンバ
        [Browsable(false)]
        public virtual void Dispose()
        {
        }
        #endregion
    }

}
