using System;
using System.Collections.Generic;
using System.Text;
using System.Globalization;
using System.Xml.Serialization;
using System.IO;
using System.Xml;
using Misuzilla.Net.Irc;
using System.Diagnostics;

namespace Misuzilla.Applications.TwitterIrcGateway
{
    public class Groups : SortedList<string, Group>
    {
        public Groups()
            : base(StringComparer.InvariantCultureIgnoreCase)
        {
        }

        private static Object _syncObject = new object();
        private static XmlSerializer _serializer = null;
        static Groups()
        {
            lock (_syncObject)
            {
                if (_serializer == null)
                {
                    _serializer = new XmlSerializer(typeof(Group[]));
                }
            }
        }
        private static XmlSerializer Serializer
        {
            get
            {
                return _serializer;
            }
        }

        public void Serialize(Stream stream)
        {
            Group[] groups = new Group[this.Values.Count];
            this.Values.CopyTo(groups, 0);
            using (XmlTextWriter xmlTextWriter = new XmlTextWriter(stream, Encoding.UTF8))
            {
                _serializer.Serialize(xmlTextWriter, groups);
            }
        }

        public static Groups Deserialize(Stream stream)
        {
            Group[] groups = _serializer.Deserialize(stream) as Group[];
            Groups retGroups = new Groups();
            foreach (Group group in groups)
            {
                retGroups[group.Name] = group;
                group.IsJoined = false;
                group.ChannelModes = group.ChannelModes == null ? new List<ChannelMode>() : group.ChannelModes;
            }

            return retGroups;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="path"></param>
        /// <returns></returns>
        public static Groups Load(String path)
        {
            // group 読み取り
            if (File.Exists(path))
            {
                Trace.WriteLine(String.Format("Load Group: {0}", path));
                try
                {
                    using (FileStream fs = new FileStream(path, FileMode.Open))
                    {
                        try
                        {
                            Groups groups = Groups.Deserialize(fs);
                            if (groups != null)
                                return groups;
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
            return new Groups();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="path"></param>
        public void Save(String path)
        {
            Trace.WriteLine(String.Format("Save Group: {0}", path));
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

    public class Group : IComparable
    {
        public String Name { get; set; }
        public String Mode { get; set; }
        public List<String> Members { get; set; }
        public Boolean IsJoined { get; set; }
        public Boolean IsSpecial { get; set; }
        public String Topic { get; set; }
        public List<ChannelMode> ChannelModes { get; set; }

        public Group()
        {
            ChannelModes = new List<ChannelMode>();
        }

        public Group(String name)
        {
            if (!name.StartsWith("#") || name.Length < 2)
            {
                throw new ArgumentException("チャンネル名は#で始まる必要があります。");
            }
            Name = name;
            Members = new List<string>();
            ChannelModes = new List<ChannelMode>();
        }

        public Boolean Exists(String id)
        {
            Int32 pos;
            lock (Members)
            {
                pos = Members.BinarySearch(id, StringComparer.InvariantCultureIgnoreCase);
            }
            return pos > -1;
        }

        public void Add(String id)
        {
            lock (Members)
            {
                Members.Add(id);
                Members.Sort(StringComparer.InvariantCultureIgnoreCase);
            }
        }

        public void Remove(String id)
        {
            lock (Members)
            {
                Members.Remove(id);
                Members.Sort(StringComparer.InvariantCultureIgnoreCase);
            }
        }

        public Boolean IgnoreEchoBack
        {
            get
            {
                return ChannelModes.Exists(mode => mode.Mode == ChannelModeTypes.Private);
            }
        }

        public Boolean IsOrMatch
        {
            get
            {
                return String.IsNullOrEmpty(Topic) ? false : Topic.StartsWith("|");
            }
        }

        public override string ToString()
        {
            return String.Format("Group: {0} ({1} members)", Name, Members.Count);
        }

        #region IComparable メンバ

        public int CompareTo(object obj)
        {
            if (!(obj is Group))
                return -1;

            return String.Compare((obj as Group).Name, this.Name, true, CultureInfo.InvariantCulture);
        }

        #endregion
    }

    public class RoutedGroup : IComparable
    {
        public Group Group { get; set; }
        public Boolean IsMessageFromSelf { get; set; }
        public Boolean IsExistsInChannelOrNoMembers { get; set; }
        public String IRCMessageType { get; set; }
        public String Text { get; set; }
        
        public RoutedGroup()
        {
            IRCMessageType = "PRIVMSG";
        }
        
        #region IComparable メンバ
        public int CompareTo(object obj)
        {
            if (!(obj is RoutedGroup))
                return -1;

            return String.Compare((obj as RoutedGroup).Group.Name, this.Group.Name, true, CultureInfo.InvariantCulture);
        }
        #endregion
    }
}
