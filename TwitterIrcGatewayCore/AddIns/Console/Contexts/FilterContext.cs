using System;
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
        
        [Description("フィルタを再読み込みします")]
        public void Reload()
        {
            Session.LoadFilters();
            ConsoleAddIn.NotifyMessage("フィルタを再読み込みしました。");
        }

        private void SwitchEnable(String args, Boolean enable)
        {
            Int32 index;
            FilterItem[] items = Session.Filters.Items;
            if (Int32.TryParse(args, out index))
            {
                if (index < items.Length && index > -1)
                {
                    items[index].Enabled = enable;
                    Session.SaveFilters();
                    ConsoleAddIn.NotifyMessage(String.Format("フィルタ {0} を{1}化しました。", items[index], (enable ? "有効" : "無効")));
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
}
