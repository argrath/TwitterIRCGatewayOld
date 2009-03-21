using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Reflection;
using System.Text;
using Misuzilla.Net.Irc;
using System.Collections;

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
        public ConsoleAddIn ConsoleAddIn { get { return Session.AddInManager.GetAddIn<ConsoleAddIn>(); } }

        public virtual Type[] Contexts { get { return new Type[0]; } }
        public virtual IConfiguration[] Configurations { get { return new IConfiguration[0]; } }
        
        /// <summary>
        /// 
        /// </summary>
        [Browsable(false)]
        public virtual void Initialize()
        {
        }

        /// <summary>
        /// 設定が変更された際に行う処理
        /// </summary>
        /// <param name="config"></param>
        /// <param name="memberInfo"></param>
        /// <param name="value"></param>
        protected virtual void OnConfigurationChanged(IConfiguration config, MemberInfo memberInfo, Object value)
        {
        }

        #region Context Base Implementation
        [Description("コマンドの一覧または説明を表示します")]
        public void Help([Description("コマンド名")]String commandName)
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

        [Description("設定を表示します")]
        public void Show([Description("設定項目名(指定しない場合にはすべて表示)")]String configName)
        {
            foreach (var config in Configurations)
            {
                MemberInfo[] memberInfoArr;
                
                // プロパティ一覧または一つだけ
                if (String.IsNullOrEmpty(configName))
                    memberInfoArr = config.GetType().GetMembers(BindingFlags.Public | BindingFlags.IgnoreCase | BindingFlags.Instance);
                else
                    memberInfoArr = config.GetType().GetMember(configName, BindingFlags.Public | BindingFlags.IgnoreCase | BindingFlags.Instance);
                
                foreach (var memberInfo in memberInfoArr)
                {
                    if (!IsBrowsable(memberInfo))
                        continue;

                    PropertyInfo pi = memberInfo as PropertyInfo;
                    FieldInfo fi = memberInfo as FieldInfo;

                    if (pi != null && pi.CanWrite)
                        ConsoleAddIn.NotifyMessage(String.Format("{0} ({1}) = {2}", pi.Name, pi.PropertyType.Name, Inspect(pi.GetValue(config, null))));
                    else if (fi != null && !fi.IsInitOnly)
                        ConsoleAddIn.NotifyMessage(String.Format("{0} ({1}) = {2}", fi.Name, fi.FieldType.Name, Inspect(fi.GetValue(config))));

                    // さがしているのが一個の時は説明を出して終わり
                    if (!String.IsNullOrEmpty(configName))
                    {
                        String desc = GetDescription(memberInfo);
                        if (!String.IsNullOrEmpty(desc))
                            ConsoleAddIn.NotifyMessage(desc);
                        return;
                    }
                }
            }
            if (!String.IsNullOrEmpty(configName))
                ConsoleAddIn.NotifyMessage(String.Format("設定項目 \"{0}\" は存在しません。", configName));
        }
        
        [Description("設定を変更します")]
        public void Set([Description("設定項目名")]String configName, [Description("設定する値")]String value)
        {
            if (String.IsNullOrEmpty(configName))
            {
                ConsoleAddIn.NotifyMessage("設定名が指定されていません。");
                return;
            }
            if (String.IsNullOrEmpty(value))
            {
                ConsoleAddIn.NotifyMessage("値が指定されていません。");
                return;
            }
            
            foreach (var config in Configurations)
            {
                MemberInfo[] memberInfoArr = config.GetType().GetMember(configName, BindingFlags.Public | BindingFlags.IgnoreCase | BindingFlags.Instance);
                
                foreach (var memberInfo in memberInfoArr)
                {
                    if (!IsBrowsable(memberInfo))
                        continue;

                    PropertyInfo pi = memberInfo as PropertyInfo;
                    FieldInfo fi = memberInfo as FieldInfo;
                    
                    if (pi == null && fi == null)
                        continue;
                    
                    // TypeConverterで文字列から変換する
                    Type type = (pi != null) ? pi.PropertyType : fi.FieldType;
                    TypeConverter tConv = TypeDescriptor.GetConverter(type);
                    if (!tConv.CanConvertFrom(typeof(String)))
                    {
                        ConsoleAddIn.NotifyMessage(String.Format("設定項目 \"{0}\" の型 \"{1}\" には適切な TypeConverter がないため、このコマンドで設定することはできません。", configName, type.FullName));
                        return;
                    }

                    try
                    {
                        Object convertedValue = tConv.ConvertFromString(value);
                        if (pi != null && pi.CanWrite)
                        {
                            pi.SetValue(config, convertedValue, null);
                            ConsoleAddIn.NotifyMessage(String.Format("{0} ({1}) = {2}", pi.Name, pi.PropertyType.Name, Inspect(pi.GetValue(config, null))));
                        }
                        else if (fi != null && !fi.IsInitOnly)
                        {
                            fi.SetValue(config, value);
                            ConsoleAddIn.NotifyMessage(String.Format("{0} ({1}) = {2}", fi.Name, fi.FieldType.Name, Inspect(fi.GetValue(config))));
                        }
                        OnConfigurationChanged(config, memberInfo, value);
                    }
                    catch (Exception ex)
                    {
                        ConsoleAddIn.NotifyMessage(String.Format("設定項目 \"{0}\" の型 \"{1}\" に値を変換し設定する際にエラーが発生しました({2})。", configName, type.FullName, ex.GetType().Name));
                        foreach (var line in ex.Message.Split('\n'))
                            ConsoleAddIn.NotifyMessage(line);
                    }

                    // 見つかったので値をセットして終わり
                    return;
                }
            }
            
            ConsoleAddIn.NotifyMessage(String.Format("設定項目 \"{0}\" は存在しません。", configName));
        }

        [Description("コンテキストを一つ前のものに戻します")]
        public void Exit()
        {
            ConsoleAddIn.PopContext();
        }
        #endregion

        #region Context Helpers
        
        [Browsable(false)]
        private String Inspect(Object o)
        {
            if (o == null)
                return "(null)";
            if (o is String)
                return o.ToString();
            
            if (o is IEnumerable)
            {
                StringBuilder sb = new StringBuilder();
                foreach (var item in (IEnumerable)o)
                {
                    sb.Append(o.ToString()).Append(", ");
                }
                if (sb.Length > 0)
                    sb.Length += 2;
                return sb.ToString();
            }
            else
            {
                return o.ToString();
            }
        }

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
