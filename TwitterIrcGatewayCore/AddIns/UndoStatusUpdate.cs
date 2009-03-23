using System;
using System.Collections.Generic;
using System.Net;
using System.Text;
using Misuzilla.Net.Irc;

namespace Misuzilla.Applications.TwitterIrcGateway.AddIns
{
    class UndoStatusUpdate : AddInBase
    {
        private List<Status> _lastUpdateStatusList = new List<Status>();
        private const String UndoCommandString = "undo";
        private const Int32 MaxUndoCount = 10;
        
        public override void Initialize()
        {
            Session.PostSendUpdateStatus += (sender, e) =>
                                            {
                                                _lastUpdateStatusList.Add(e.Status);
                                                if (_lastUpdateStatusList.Count > MaxUndoCount)
                                                {
                                                    _lastUpdateStatusList.RemoveAt(0);
                                                }
                                            };
            Session.PreSendUpdateStatus += (sender, e) =>
                                           {
                                            if (String.Compare(e.Text.Trim(), UndoCommandString, true) == 0)
                                            {
                                                e.Cancel = true;

                                                if (_lastUpdateStatusList.Count == 0)
                                                {
                                                    Session.SendServer(new NoticeMessage(e.ReceivedMessage.Receiver, "ステータスアップデートの取り消しできません。"));
                                                    return;
                                                }

                                                // 削除する
                                                try
                                                {
                                                    Status status = _lastUpdateStatusList[_lastUpdateStatusList.Count - 1];
                                                    Session.TwitterService.DestroyStatus(status.Id);
                                                    Session.SendServer(new NoticeMessage(e.ReceivedMessage.Receiver, String.Format("ステータス \"{0}\" ({1}) を削除しました。", status.Text, status.Id)));
                                                    _lastUpdateStatusList.Remove(status);
                                                }
                                                catch (TwitterServiceException te)
                                                {
                                                    Session.SendServer(new NoticeMessage(e.ReceivedMessage.Receiver, String.Format("ステータスの削除に失敗しました: {0}", te.Message)));
                                                }
                                                catch (WebException we)
                                                {
                                                    Session.SendServer(new NoticeMessage(e.ReceivedMessage.Receiver, String.Format("ステータスの削除に失敗しました: {0}", we.Message)));
                                                }
                                            }
                                           };
        }
    }
}
