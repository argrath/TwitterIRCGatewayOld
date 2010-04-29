﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using TypableMap;

namespace Misuzilla.Applications.TwitterIrcGateway.AddIns.TypableMap
{
    public interface ITypableMapStatusRepository
    {
        void SetSize(Int32 size);
        String Add(Status status);
        Boolean TryGetValue(String typableMapId, out Status status);
    }
    
    public class TypableMapStatusMemoryRepository : ITypableMapStatusRepository
    {
        private TypableMap<Status> _typableMap;
        public TypableMapStatusMemoryRepository(Int32 size)
        {
            _typableMap = new TypableMap<Status>(size);
        }

        #region ITypableMapStatusRepository メンバ
        public void SetSize(int size)
        {
            _typableMap = new TypableMap<Status>(size);
        }

        public String Add(Status status)
        {
            return _typableMap.Add(status);
        }

        public Boolean TryGetValue(String typableMapId, out Status status)
        {
            return _typableMap.TryGetValue(typableMapId, out status);
        }
        #endregion
    }


    public class TypableMapStatusMemoryRepository2 : ITypableMapStatusRepository
    {
        private TypableMap<StorageItem<Status>> _typableMap;
        private static Storage<Int64, Status> _storageStatus = new Storage<Int64, Status>(v => v.Id, 100);

        public TypableMapStatusMemoryRepository2(Int32 size)
        {
            _typableMap = new TypableMap<StorageItem<Status>>(size);
        }

        #region TypableMapStatusMemoryRepository2 メンバ
        public void SetSize(int size)
        {
            _typableMap = new TypableMap<StorageItem<Status>>(size);
        }

        public String Add(Status status)
        {
            StorageItem<Status> storageItem = _storageStatus.AddOrUpdate(status);
            return _typableMap.Add(storageItem);
        }

        public Boolean TryGetValue(String typableMapId, out Status status)
        {
            StorageItem<Status> storageItem;
            if (_typableMap.TryGetValue(typableMapId, out storageItem))
            {
                status = storageItem.Value;
                return true;
            }

            status = null;
            return false;
        }
        #endregion


        public class Storage<TKey, TValue> where TValue : class
        {
            private Dictionary<TKey, WeakReference> _storage;
            private Func<TValue, TKey> _keySelector;
            private Int32 _compactionSize;

            public Storage(Func<TValue, TKey> keySelector) : this(keySelector, 1000)
            {}
            public Storage(Func<TValue, TKey> keySelector, Int32 compactionSize)
            {
                _keySelector = keySelector;
                _storage = new Dictionary<TKey, WeakReference>();
                _compactionSize = compactionSize;
            }

            public Int32 Count
            {
                get { return _storage.Count; }
            }

            public Int32 AliveCount
            {
                get { return _storage.Where(kv => kv.Value.IsAlive).Count(); }
            }

            public void Compact()
            {
                lock (_storage)
                {
                    foreach (var key in _storage.Where(kv => !kv.Value.IsAlive).Select(kv => kv.Key).ToArray())
                    {
                        _storage.Remove(key);
                    }
                }
            }

            public StorageItem<TValue> Get(TKey key)
            {
                lock (_storage)
                {
                    WeakReference weakReference;
                    if (_storage.TryGetValue(key, out weakReference) && weakReference.IsAlive)
                    {
                        return weakReference.Target as StorageItem<TValue>;
                    }
                    return null;
                }
            }

            public TValue GetValue(TKey key)
            {
                lock (_storage)
                {
                    StorageItem<TValue> item = Get(key);

                    if (item != null)
                        return item.Value;

                    return null;
                }
            }

            public StorageItem<TValue> AddOrUpdate(TValue value)
            {
                lock (_storage)
                {
                    if (_storage.Count > _compactionSize)
                        Compact();

                    TKey key = _keySelector(value);
                    StorageItem<TValue> item = null;
                    if (_storage.ContainsKey(key))
                    {
                        WeakReference weakRef = _storage[key];

                        if (weakRef.IsAlive)
                        {
                            item = weakRef.Target as StorageItem<TValue>;
                        }
                    }

                    if (item == null)
                    {
                        item = new StorageItem<TValue>();
                        _storage[key] = new WeakReference(item);
                    }

                    item.Value = value;
                    return item;
                }
            }
        }
        public class StorageItem<T> where T : class
        {
            public T Value { get; set; }

            public StorageItem()
                : this(null)
            {
            }

            public StorageItem(T value)
            {
                Value = value;
            }
        }

        private User CloneUser(User originalUser)
        {
            return new User
                       {
                            Description = originalUser.Description,
                            Id= originalUser.Id,
                            Location = originalUser.Location,
                            Name = originalUser.Name,
                            ProfileImageUrl = originalUser.ProfileImageUrl,
                            Protected = originalUser.Protected,
                            ScreenName = originalUser.ScreenName,
                            Status = null,
                            Url = originalUser.Url
                       };
        }
        private Status CloneStatus(Status originalStatus)
        {
            return new Status
                       {
                           Id = originalStatus.Id,
                           _createdAtOriginal = originalStatus._createdAtOriginal,
                           _textOriginal = originalStatus._textOriginal,
                           Favorited = originalStatus.Favorited,
                           InReplyToStatusId = originalStatus.InReplyToStatusId,
                           InReplyToUserId = originalStatus.InReplyToUserId,
                           RetweetedStatus = null,
                           Source = originalStatus.Source,
                           Truncated = originalStatus.Truncated,
                           User = null
                       };
        }
    }
}
