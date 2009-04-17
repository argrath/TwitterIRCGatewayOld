using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Xml;
using System.Xml.Serialization;
using Misuzilla.Applications.TwitterIrcGateway.AddIns;

namespace Misuzilla.Applications.TwitterIrcGateway
{
    /// <summary>
    /// アドインを管理する機能を提供します。
    /// </summary>
    public class AddInManager : MarshalByRefObject
    {
        private List<IAddIn> _addIns;
        private List<Type> _addInTypes;
        private List<Type> _configurationTypes;
        private XmlSerializer _xmlSerializer;
        private Session _session;
        private Server _server;
        private AppDomain _addInDomain;

        /// <summary>
        /// 読み込まれているアドインのコレクションを取得します
        /// </summary>
        public ICollection<IAddIn> AddIns { get { return _addIns.AsReadOnly(); } }
        
        /// <summary>
        /// アドインの型のコレクションを取得します
        /// </summary>
        public ICollection<Type> AddInTypes { get { return _addInTypes.AsReadOnly(); } }

        /// <summary>
        /// <see cref="AddInManager"/> クラスのインスタンスを初期化します。
        /// </summary>
        /// <param name="server">サーバのインスタンス</param>
        /// <param name="session">接続中のセッション情報のインスタンス</param>
        public AddInManager(Server server, Session session)
        {
            _session = session;
            _server = server;
        }

        //public static AddInManager CreateInstanceWithAppDomain(Server server, Session session)
        //{
        //    AppDomain addInDomain = AppDomain.CreateDomain("AddInDomain-" + session.GetHashCode());
        //    AddInManager addInManager = addInDomain.CreateInstanceAndUnwrap(typeof(AddInManager).Assembly.FullName, typeof(AddInManager).FullName, false,
        //                               BindingFlags.Public | BindingFlags.CreateInstance | BindingFlags.Instance, null, new object[] {server, session}, null, null, null) as AddInManager;
        //    addInDomain.UnhandledException += (sender, e) => {
        //        Trace.WriteLine(e.ExceptionObject.ToString());
        //    };
        //    return addInManager;
        //}

        /// <summary>
        /// アドインを読み込みます。
        /// </summary>
        public void Load()
        {
            _addInTypes = new List<Type>();
            _addIns = new List<IAddIn>();
            _configurationTypes = new List<Type>();

            LoadAddInFromAssembly(Assembly.GetExecutingAssembly());
            
            String addinsBase = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "AddIns");
            if (Directory.Exists(addinsBase))
            {
                foreach (String fileName in Directory.GetFiles(addinsBase, "*.dll"))
                {
                    // 無視する
                    if (String.Compare(Path.GetFileName(fileName), "Misuzilla.Applications.TwitterIrcGateway.AddIns.DLRIntegration.dll", true) == 0)
                        continue;

                    try
                    {
                        Assembly asm = Assembly.LoadFile(fileName);
                        LoadAddInFromAssembly(asm);
                    }
                    catch (Exception e)
                    {
                    }
                }
            }

            Initialize();
        }

        /// <summary>
        /// アドインを初期化します。
        /// </summary>
        public void Initialize()
        {
            foreach (Type addInType in _addInTypes)
            {
                if (_session.Config.DisabledAddInsList.Contains(addInType.FullName))
                {
                    Trace.WriteLine(String.Format("AddIn[Disabled]: {0}", addInType.FullName));
                    continue;
                }

                Trace.WriteLine(String.Format("AddIn: {0}", addInType.FullName));
                _addIns.Add(Activator.CreateInstance(addInType) as IAddIn);
            }

            // XMLのシリアライザの中で名前がかぶらないようにする
            XmlAttributeOverrides xmlAttrOverrides = new XmlAttributeOverrides();
            foreach (var configType in _configurationTypes)
            {
                XmlAttributes xmlAttributes = new XmlAttributes();
                xmlAttributes.XmlType = new XmlTypeAttribute(configType.FullName);
                xmlAttrOverrides.Add(configType, xmlAttributes);
            }
            _xmlSerializer = new XmlSerializer(typeof(Object), xmlAttrOverrides, _configurationTypes.ToArray(), null, null);

            foreach (IAddIn addIn in _addIns)
                addIn.Initialize(_server, _session);
        }

        /// <summary>
        /// 読み込まれているアドインを破棄します。
        /// </summary>
        public void Uninitialize()
        {
            foreach (IAddIn addIn in _addIns)
                addIn.Uninitialize();

            _addIns = new List<IAddIn>();
        }
        
        /// <summary>
        /// 指定した型を設定ファイルから読み込みます
        /// </summary>
        /// <param name="configType">設定の型</param>
        /// <returns>設定のインスタンス</returns>
        public Object GetConfig(Type configType)
        {
            String fileName = configType.FullName + ".xml";
            String path = Path.Combine(Path.Combine(_session.UserConfigDirectory, "AddIns"), fileName);
            
            if (File.Exists(path))
            {
                Trace.WriteLine(String.Format("Load Configuration: {0}", path));
                try
                {
                    using (FileStream fs = new FileStream(path, FileMode.Open))
                    {
                        try
                        {
                            Object retVal = _xmlSerializer.Deserialize(fs);
                            if (retVal != null)
                                return retVal;
                        }
                        catch (XmlException xe)
                        {
                            Trace.WriteLine(xe.Message);
                        }
                        catch (InvalidOperationException ioe)
                        {
                            Trace.WriteLine(ioe.Message);
                        }
                    }
                }
                catch (IOException ie)
                {
                    Trace.WriteLine(ie.Message);
                    throw;
                }
            }
            return null;
        }

        /// <summary>
        /// 指定した型を設定ファイルから読み込みます
        /// </summary>
        /// <returns>指定した設定型のインスタンス</returns>
        public T GetConfig<T>() where T : class, IConfiguration, new()
        {
            T retVal = GetConfig(typeof(T)) as T;
            return retVal ?? new T();
        }

        /// <summary>
        /// オブジェクトを設定ファイルに書き込みます
        /// </summary>
        /// <param name="o">保存する設定のインスタンス</param>
        public void SaveConfig(IConfiguration o)
        {
            Type configType = o.GetType();

            if (!_configurationTypes.Contains(configType))
                throw new ArgumentException("指定されたオブジェクトは設定ファイルとして登録されていない型です。");
            
            String fileName = configType.FullName + ".xml";
            String path = Path.Combine(Path.Combine(_session.UserConfigDirectory, "AddIns"), fileName);

            lock (o)
            {
                Trace.WriteLine(String.Format("Save Configuration: {0}", path));
                try
                {
                    String dir = Path.GetDirectoryName(path);
                    Directory.CreateDirectory(dir);
                    using (FileStream fs = new FileStream(path, FileMode.Create))
                    {
                        try
                        {
                            _xmlSerializer.Serialize(fs, o);
                        }
                        catch (XmlException xe)
                        {
                            Trace.WriteLine(xe.Message);
                            throw;
                        }
                        catch (InvalidOperationException ioe)
                        {
                            Trace.WriteLine(ioe.Message);
                            throw;
                        }
                    }
                }
                catch (IOException ie)
                {
                    Trace.WriteLine(ie.Message);
                    throw;
                }
            }
        }

        /// <summary>
        /// 指定した型の設定を初期化します。
        /// </summary>
        /// <typeparam name="T">初期化対象の設定型</typeparam>
        public void ResetConfig<T>() where T : class, IConfiguration, new()
        {
            T newObj = new T();
            SaveConfig(newObj);
        }
        
        /// <summary>
        /// 指定した型のアドインのインスタンスを取得します
        /// </summary>
        /// <param name="t">取得したいアドインの型</param>
        /// <returns>アドインのインスタンス</returns>
        public IAddIn GetAddIn(Type t)
        {
            foreach (var addIn in _addIns)
            {
                if (addIn.GetType() == t)
                    return addIn;
            }
            return null;
        }
        
        /// <summary>
        /// 指定した型のアドインのインスタンスを取得します
        /// </summary>
        /// <typeparam name="T">取得したいアドインの型</typeparam>
        /// <returns>アドインのインスタンス</returns>
        public T GetAddIn<T>() where T : class, IAddIn
        {
            return GetAddIn(typeof(T)) as T;
        }

        /// <summary>
        /// アドインを初期化して実行し直します。ファイルからの読み込みは行われません。
        /// </summary>
        public void RestartAddIns()
        {
            Uninitialize();
            Initialize();
            
            _session.OnAddInsLoadCompleted();
        }

        #region Helper Methods

        private void LoadAddInFromAssembly(Assembly asm)
        {
            Type addinType = typeof(IAddIn);
            Type configurationType = typeof(IConfiguration);
            foreach (Type t in asm.GetTypes())
            {
                if (addinType.IsAssignableFrom(t) && !t.IsAbstract && t.IsClass)
                {
                    // IAddIn
                    _addInTypes.Add(t);
                }
                else if (configurationType.IsAssignableFrom(t) && !t.IsAbstract && t.IsClass)
                {
                    // IConfiguration
                    _configurationTypes.Add(t);
                }
            }
        }

        #endregion
    }
}
