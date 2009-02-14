using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Reflection;
using System.Diagnostics;
using System.Xml;
using Misuzilla.Applications.TwitterIrcGateway.AddIns;
using System.Xml.Serialization;

namespace Misuzilla.Applications.TwitterIrcGateway
{
    public class AddInManager
    {
        private List<IAddIn> _addIns = new List<IAddIn>();
        private List<Type> _configurationTypes = new List<Type>();
        private XmlSerializer _xmlSerializer;
        private Session _session;

        public ICollection<IAddIn> AddIns { get { return _addIns.AsReadOnly(); } }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="server"></param>
        /// <param name="session"></param>
        public void Load(Server server, Session session)
        {
            _session = session;
            
            LoadAddInFromAssembly(Assembly.GetExecutingAssembly());
            
            String addinsBase = Path.Combine(Path.GetDirectoryName(Assembly.GetEntryAssembly().Location), "AddIns");
            if (Directory.Exists(addinsBase))
            {
                foreach (String fileName in Directory.GetFiles(addinsBase, "*.dll"))
                {
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

            _xmlSerializer = new XmlSerializer(typeof(Object), _configurationTypes.ToArray());

            foreach (IAddIn addIn in _addIns)
                addIn.Initialize(server, session);
        }
        
        /// <summary>
        /// 指定した型を設定ファイルから読み込みます
        /// </summary>
        /// <param name="configType"></param>
        /// <returns></returns>
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
        /// <returns></returns>
        public T GetConfig<T>() where T : class, IConfiguration, new()
        {
            T retVal = GetConfig(typeof(T)) as T;
            return retVal ?? new T();
        }

        /// <summary>
        /// オブジェクトを設定ファイルに書き込みます
        /// </summary>
        /// <param name="o"></param>
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
        }

        /// <summary>
        /// アドインを取得します
        /// </summary>
        /// <param name="t"></param>
        /// <returns></returns>
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
        /// アドインを取得します
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <returns></returns>
        public T GetAddIn<T>() where T : class, IAddIn
        {
            return GetAddIn(typeof(T)) as T;
        }
        
        
        private void LoadAddInFromAssembly(Assembly asm)
        {
            Type addinType = typeof(IAddIn);
            Type configurationType = typeof(IConfiguration);
            foreach (Type t in asm.GetTypes())
            {
                if (addinType.IsAssignableFrom(t) && !t.IsAbstract && t.IsClass)
                {
                    Trace.WriteLine(String.Format("Load AddIn: {0}", t));
                    IAddIn addIn = Activator.CreateInstance(t) as IAddIn;

                    _addIns.Add(addIn);
                }
                else if (configurationType.IsAssignableFrom(t) && !t.IsAbstract && t.IsClass)
                {
                    // IConfiguration
                    _configurationTypes.Add(t);
                }
            }
        }
    }
}
