using System;
using System.Collections.Generic;
using System.Text;
using System.Globalization;
using System.Xml.Serialization;
using System.IO;
using System.Xml;
using Misuzilla.Net.Irc;

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
    }

    public class Group : IComparable
    {
        public String Name { get; set; }
        public String Mode { get; set; }
        public List<String> Members { get; set; }
        public Boolean IsJoined { get; set; }
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

        #region IComparable メンバ

        public int CompareTo(object obj)
        {
            if (!(obj is Group))
                return -1;

            return String.Compare((obj as Group).Name, this.Name, true, CultureInfo.InvariantCulture);
        }

        #endregion
    }
}
