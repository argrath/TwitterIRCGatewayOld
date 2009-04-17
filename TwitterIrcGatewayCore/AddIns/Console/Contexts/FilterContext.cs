﻿using System;
using System.ComponentModel;
using Misuzilla.Applications.TwitterIrcGateway.Filter;

namespace Misuzilla.Applications.TwitterIrcGateway.AddIns.Console
{
    /// <summary>
    /// 
    /// </summary>
    [Description("フィルタの設定を行うコンテキストに切り替えます")]
    public class FilterContext : Context
    {
        [Description("存在するフィルタをすべて表示します")]
        public void List()
        {
            for (var i = 0; i < Session.Filters.Items.Length; i++)
            {
                FilterItem filter = Session.Filters.Items[i];
                ConsoleAddIn.NotifyMessage(String.Format("{0}: {1}", i, filter.ToString()));
            }
        }

        [Description("指定したフィルタを有効化します")]
        public void Enable(String args)
        {
            SwitchEnable(args, true);
        }

        [Description("指定したフィルタを無効化します")]
        public void Disable(String args)
        {
            SwitchEnable(args, false);
        }
        
        [Description("指定したフィルタを削除します")]
        public void Remove(String args)
        {
            FindAt(args, item => {
                Session.Filters.Remove(item);
                Session.SaveFilters();
                ConsoleAddIn.NotifyMessage(String.Format("フィルタ {0} を削除しました。", item));
            });
        }
        
        [Description("指定したフィルタを編集します")]
        public void Edit(String args)
        {
            FindAt(args, item =>
                             {
                                 Type genericType = typeof (EditFilterContext<>).MakeGenericType(item.GetType());
                                 Context ctx = Activator.CreateInstance(genericType, item) as Context;
                                 ctx.Server = Server;
                                 ctx.Session = Session;

                                 ConsoleAddIn.PushContext(ctx);
                             });
        }

        [Description("指定した種類のフィルタを新規追加します")]
        public void New(String filterTypeName)
        {
            Type filterType = Type.GetType("Misuzilla.Applications.TwitterIrcGateway.Filter."+filterTypeName, false, true);
            if (filterType == null || !filterType.IsSubclassOf(typeof(FilterItem)))
            {
                ConsoleAddIn.NotifyMessage("不明なフィルタの種類が指定されました。");
                return;
            }
            Type genericType = typeof (EditFilterContext<>).MakeGenericType(filterType);
            ConsoleAddIn.PushContext(ConsoleAddIn.GetContext(genericType, Server, Session));
        }

        [Description("フィルタを再読み込みします")]
        public void Reload()
        {
            Session.LoadFilters();
            ConsoleAddIn.NotifyMessage("フィルタを再読み込みしました。");
        }

        private void SwitchEnable(String args, Boolean enable)
        {
            FindAt(args, item => {
                item.Enabled = enable;
                Session.SaveFilters();
                ConsoleAddIn.NotifyMessage(String.Format("フィルタ {0} を{1}化しました。", item, (enable ? "有効" : "無効")));
            });
        }
        private void FindAt(String args, Action<FilterItem> action)
        {
            Int32 index;
            FilterItem[] items = Session.Filters.Items;
            if (Int32.TryParse(args, out index))
            {
                if (index < items.Length && index > -1)
                {
                    action(items[index]);
                }
                else
                {
                    ConsoleAddIn.NotifyMessage("存在しないフィルタが指定されました。");
                }
            }
            else
            {
                ConsoleAddIn.NotifyMessage("フィルタの指定が正しくありません。");
            }
        }
    }

    /// <summary>
    /// 編集用のジェネリックコンテキスト
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public class EditFilterContext<T> : Context where T : FilterItem, new()
    {
        private Boolean _isNewRecord;
        private T _filter;

        public override string ContextName
        {
            get
            {
                return (_isNewRecord ? "New" : "Edit") + typeof (T).Name;
            }
        }
        
        public EditFilterContext()
        {
            _filter = new T();
            _isNewRecord = true;
        }
        
        public EditFilterContext(T filterItem)
        {
            _filter = filterItem;
            _isNewRecord = false;
        }
        
        public override IConfiguration[] Configurations
        {
            get
            {
                return new IConfiguration[] { _filter };
            }
        }

        [Description("フィルタを保存してコンテキストを終了します")]
        public void Save()
        {
            Session.Filters.Add(_filter);
            Session.SaveFilters();
            ConsoleAddIn.NotifyMessage(String.Format("フィルタを{0}しました。", (_isNewRecord ? "新規作成" : "保存")));
            Exit();
        }
    }
}
