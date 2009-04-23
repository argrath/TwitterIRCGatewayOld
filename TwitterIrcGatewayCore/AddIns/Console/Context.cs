using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Reflection;
using System.Text;

namespace Misuzilla.Applications.TwitterIrcGateway.AddIns.Console
{
    public class ContextInfo
    {
        public Type Type { get; set; }
        public String DisplayName { get; set; }
        public String Description { get; set; }
    
        public ContextInfo()
        {
        }
    
        public ContextInfo(Type t)
        {
            Type = t;
            DisplayName = t.Name;
            Description = AttributeUtil.GetDescription(t);
        }
    }
    
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
        [Obsolete("ConsoleAddIn プロパティは古い形式です。Console プロパティを利用してください。このプロパティは常に ConsoleAddIn クラスの唯一のインスタンスを返します。")]
        public ConsoleAddIn ConsoleAddIn { get { return Session.AddInManager.GetAddIn<ConsoleAddIn>(); } }
        [Browsable(false)]
        public Console Console { get; internal set; }

        public virtual ICollection<ContextInfo> Contexts { get { return new List<ContextInfo>().AsReadOnly(); } }
        public virtual IConfiguration[] Configurations { get { return new IConfiguration[0]; } }
        public virtual String ContextName { get { return this.GetType().Name; } }
        
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
        
        /// <summary>
        /// 存在しないコマンドが呼ばれた場合の処理
        /// </summary>
        /// <param name="commandName"></param>
        /// <param name="rawInputLine"></param>
        /// <returns>コマンドを処理したかどうかを表す値。falseを返す場合、該当するコマンドは存在しなかった扱いとなります。</returns>
        [Browsable(false)]
        public virtual Boolean OnCallMissingCommand(String commandName, String rawInputLine)
        {
            return false;
        }

        #region Context Base Implementation
        [Description("コマンドの一覧または説明を表示します")]
        public virtual void Help([Description("コマンド名")]String commandName)
        {
            if (String.IsNullOrEmpty(commandName))
            {
                // コマンドの一覧
                if (Contexts.Count > 0)
                {
                    Console.NotifyMessage("[Contexts]");
                    foreach (var ctxInfo in Contexts)
                    {
                        if (AttributeUtil.IsBrowsable(ctxInfo.Type))
                            Console.NotifyMessage(String.Format("{0} - {1}", ctxInfo.DisplayName.Replace("Context", ""), ctxInfo.Description));
                    }
                }

                Console.NotifyMessage("[Commands]");
                foreach (var command in GetCommands())
                {
                    Console.NotifyMessage(String.Format("{0} - {1}", command.Key, command.Value));
                }
            }
            else
            {
                // コマンドの説明
                MethodInfo methodInfo = GetCommand(commandName);
                if (methodInfo == null)
                {
                    Console.NotifyMessage("指定された名前はこのコンテキストのコマンドに見つかりません。");
                    return;
                }

                String desc = AttributeUtil.GetDescription(methodInfo);
                if (!String.IsNullOrEmpty(desc))
                    Console.NotifyMessage(desc);
                
                ParameterInfo[] paramInfo = methodInfo.GetParameters();
                if (paramInfo.Length > 0)
                {
                    Console.NotifyMessage("引数:");
                    foreach (var paramInfoItem in paramInfo)
                    {
                        desc = AttributeUtil.GetDescription(paramInfoItem);
                        Console.NotifyMessage(String.Format("- {0}: {1}",
                                                                 (String.IsNullOrEmpty(desc) ? paramInfoItem.Name : desc),
                                                                 paramInfoItem.ParameterType));
                    }
                }
            }
        }

        [Description("設定を表示します")]
        public virtual void Show([Description("設定項目名(指定しない場合にはすべて表示)")]String configName)
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
                    if (!AttributeUtil.IsBrowsable(memberInfo))
                        continue;

                    PropertyInfo pi = memberInfo as PropertyInfo;
                    FieldInfo fi = memberInfo as FieldInfo;

                    if (pi != null && pi.CanWrite)
                        Console.NotifyMessage(String.Format("{0} ({1}) = {2}", pi.Name, pi.PropertyType.Name, Inspect(pi.GetValue(config, null))));
                    else if (fi != null && !fi.IsInitOnly)
                        Console.NotifyMessage(String.Format("{0} ({1}) = {2}", fi.Name, fi.FieldType.Name, Inspect(fi.GetValue(config))));

                    // さがしているのが一個の時は説明を出して終わり
                    if (!String.IsNullOrEmpty(configName))
                    {
                        String desc = AttributeUtil.GetDescription(memberInfo);
                        if (!String.IsNullOrEmpty(desc))
                            Console.NotifyMessage(desc);
                        return;
                    }
                }
            }
            if (!String.IsNullOrEmpty(configName))
                Console.NotifyMessage(String.Format("設定項目 \"{0}\" は存在しません。", configName));
        }
        
        [Description("設定を変更します")]
        public virtual void Set([Description("設定項目名")]String configName, [Description("設定する値")]String value)
        {
            if (String.IsNullOrEmpty(configName))
            {
                Console.NotifyMessage("設定名が指定されていません。");
                return;
            }
            if (String.IsNullOrEmpty(value))
            {
                Console.NotifyMessage("値が指定されていません。");
                return;
            }
            
            foreach (var config in Configurations)
            {
                MemberInfo[] memberInfoArr = config.GetType().GetMember(configName, BindingFlags.Public | BindingFlags.IgnoreCase | BindingFlags.Instance);
                
                foreach (var memberInfo in memberInfoArr)
                {
                    if (!AttributeUtil.IsBrowsable(memberInfo))
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
                        Console.NotifyMessage(String.Format("設定項目 \"{0}\" の型 \"{1}\" には適切な TypeConverter がないため、このコマンドで設定することはできません。", configName, type.FullName));
                        return;
                    }

                    try
                    {
                        Object convertedValue = tConv.ConvertFromString(value);
                        if (pi != null && pi.CanWrite)
                        {
                            pi.SetValue(config, convertedValue, null);
                            Console.NotifyMessage(String.Format("{0} ({1}) = {2}", pi.Name, pi.PropertyType.Name, Inspect(pi.GetValue(config, null))));
                        }
                        else if (fi != null && !fi.IsInitOnly)
                        {
                            fi.SetValue(config, convertedValue);
                            Console.NotifyMessage(String.Format("{0} ({1}) = {2}", fi.Name, fi.FieldType.Name, Inspect(fi.GetValue(config))));
                        }
                        OnConfigurationChanged(config, memberInfo, convertedValue);
                    }
                    catch (Exception ex)
                    {
                        Console.NotifyMessage(String.Format("設定項目 \"{0}\" の型 \"{1}\" に値を変換し設定する際にエラーが発生しました({2})。", configName, type.FullName, ex.GetType().Name));
                        foreach (var line in ex.Message.Split('\n'))
                            Console.NotifyMessage(line);
                    }

                    // 見つかったので値をセットして終わり
                    return;
                }
            }
            
            Console.NotifyMessage(String.Format("設定項目 \"{0}\" は存在しません。", configName));
        }

        [Description("コマンドのエイリアスを設定します")]
        public virtual void Alias([Description("[エイリアス名] [設定するコマンド]")]String value)
        {
            String[] values = value.Trim().Split(new char[] {' '}, 2, StringSplitOptions.RemoveEmptyEntries);
            if (values.Length == 0)
            {
                // 一覧
                foreach (var alias in Console.GetAliasesByType(this.GetType()))
                {
                    Console.NotifyMessage(alias.Key + " = " + alias.Value);
                }
            }
            else if (values.Length == 1)
            {
                // 表示
                var aliasesByType = Console.GetAliasesByType(this.GetType());
                if (aliasesByType.ContainsKey(values[0]))
                {
                    Console.NotifyMessage(values[0] + " = " + aliasesByType[values[0]]);
                }
                else
                {
                    Console.NotifyMessage("エイリアスは見つかりません。");
                }
            }
            else if (values[1] == "-")
            {
                Console.UnregisterAliasByType(this.GetType(), values[0]);
                Console.NotifyMessage("エイリアス \"" + values[0] + "\" を削除しました。");
            }
            else
            {
                Console.RegisterAliasByType(this.GetType(), values[0], values[1]);
                Console.NotifyMessage("エイリアス \"" + values[0] + "\" を登録しました。");
            }
        }

        [Description("コンテキストを一つ前のものに戻します")]
        public virtual void Exit()
        {
            Console.PopContext();
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
        /// 
        /// </summary>
        /// <returns></returns>
        [Browsable(false)]
        public virtual IDictionary<String, String> GetCommands()
        {
            MethodInfo[] methodInfoArr = this.GetType().GetMethods(BindingFlags.Instance | BindingFlags.Public);
            Type t = typeof(Context);

            Dictionary<String, String> commands = new Dictionary<string, string>();
            foreach (var methodInfo in methodInfoArr)
            {
                if (t.IsAssignableFrom(methodInfo.DeclaringType) && !methodInfo.IsConstructor && !methodInfo.IsFinal && !methodInfo.IsSpecialName)
                {
                    if (!AttributeUtil.IsBrowsable(methodInfo) || (commands.ContainsKey(methodInfo.Name)))
                        continue;
                    
                    commands.Add(methodInfo.Name, AttributeUtil.GetDescription(methodInfo));
                }
            }

            return commands;
        }

        /// <summary>
        /// コマンドを名前で取得します。
        /// </summary>
        /// <param name="commandName"></param>
        [Browsable(false)]
        public virtual MethodInfo GetCommand(String commandName)
        {
            MethodInfo methodInfo = this.GetType().GetMethod(commandName,
                                                                 BindingFlags.Instance | BindingFlags.Public |
                                                                 BindingFlags.IgnoreCase);

            if (methodInfo != null &&(methodInfo.IsFinal || methodInfo.IsConstructor || methodInfo.IsSpecialName || AttributeUtil.IsBrowsable(methodInfo)))
            {
                return methodInfo;
            }

            return null;
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
