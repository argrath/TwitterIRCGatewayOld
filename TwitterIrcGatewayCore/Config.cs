using System;
using System.Collections.Generic;
using System.Text;
using System.Xml;
using System.IO;
using System.Xml.Serialization;
using System.Diagnostics;
using System.Security.Cryptography;

namespace Misuzilla.Applications.TwitterIrcGateway
{
    public class Config
    {
        public String IMServiceServerName { get; set; }
        public String IMServerName { get; set; }
        public String IMUserName { get; set; }
        public String IMEncryptoPassword { get; set; }

        public Boolean EnableTypableMap { get; set; }
        public Int32 TypableMapKeyColorNumber { get; set; }
        public Int32 TypableMapKeySize { get; set; }
        
        public Boolean EnableTrace { get; set; }

        public Boolean EnableRemoveRedundantSuffix { get; set; }

        public Object[] Configurations { get; private set; }
        
        public String GetIMPassword(String key)
        {
            StringBuilder sb = new StringBuilder();
            String passwordDecoded = Encoding.UTF8.GetString(Convert.FromBase64String(IMEncryptoPassword));
            for (var i = 0; i < passwordDecoded.Length; i++)
            {
                sb.Append((Char)(passwordDecoded[i] ^ key[i % key.Length]));
            }
            return sb.ToString();
        }

        public void SetIMPassword(String key, String password)
        {
            StringBuilder sb = new StringBuilder();
            for (var i = 0; i < password.Length; i++)
            {
                sb.Append((Char)(password[i] ^ key[i % key.Length]));
            }
            IMEncryptoPassword = Convert.ToBase64String(Encoding.UTF8.GetBytes(sb.ToString()));
        }
        
        public Config()
        {
            EnableTypableMap = false;
            TypableMapKeyColorNumber = 14;
            TypableMapKeySize = 2;
            EnableRemoveRedundantSuffix = false;
        }

        #region XML Serialize
        private static Object _syncObject = new object();
        private static XmlSerializer _serializer = null;
        static Config()
        {
            lock (_syncObject)
            {
                if (_serializer == null)
                {
                    _serializer = new XmlSerializer(typeof(Config));
                }
            }
        }
        public static XmlSerializer Serializer
        {
            get
            {
                return _serializer;
            }
        }

        public void Serialize(Stream stream)
        {
            using (XmlTextWriter xmlTextWriter = new XmlTextWriter(stream, Encoding.UTF8))
            {
                _serializer.Serialize(xmlTextWriter, this);
            }
        }

        public static Config Deserialize(Stream stream)
        {
            return _serializer.Deserialize(stream) as Config;
        }
        #endregion

        /// <summary>
        /// 
        /// </summary>
        /// <param name="path"></param>
        /// <returns></returns>
        public static Config Load(String path)
        {
            // group 読み取り
            if (File.Exists(path))
            {
                Trace.WriteLine(String.Format("Load Config: {0}", path));
                try
                {
                    using (FileStream fs = new FileStream(path, FileMode.Open))
                    {
                        try
                        {
                            Config config = Config.Deserialize(fs);
                            if (config != null)
                                return config;
                        }
                        catch (XmlException xe) { Trace.WriteLine(xe.Message); }
                        catch (InvalidOperationException ioe) { Trace.WriteLine(ioe.Message); }
                    }
                }
                catch (IOException ie)
                {
                    Trace.WriteLine(ie.Message);
                    throw;
                }
            }
            return new Config();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="path"></param>
        public void Save(String path)
        {
            Trace.WriteLine(String.Format("Save Config: {0}", path));
            try
            {
                String dir = Path.GetDirectoryName(path);
                Directory.CreateDirectory(dir);
                using (FileStream fs = new FileStream(path, FileMode.Create))
                {
                    try
                    {
                        this.Serialize(fs);
                    }
                    catch (XmlException xe) { Trace.WriteLine(xe.Message); }
                    catch (InvalidOperationException ioe) { Trace.WriteLine(ioe.Message); }
                }
            }
            catch (IOException ie)
            {
                Trace.WriteLine(ie.Message);
                throw;
            }
        }
    }
}
