import re
import clr
from Misuzilla.Applications.TwitterIrcGateway import Status, Statuses, User, Users, Utility
from Misuzilla.Applications.TwitterIrcGateway.AddIns import IConfiguration
from Misuzilla.Applications.TwitterIrcGateway.AddIns.Console import ConsoleAddIn, Console, Context
from Misuzilla.Applications.TwitterIrcGateway.AddIns.DLRIntegration import DLRIntegrationAddIn, DLRBasicConfiguration, DLRContextHelper

re_a = re.compile("(https?://www\\.amazon\\.co\\.jp/(?:.+/dp/|dp/|exec/obidos/|o/ASIN/)[^ ]+)")
re_re = re.compile("/(tag=)?[a-z0-9]+-\\d{2}|/?$")
affiliate_tag = "opentig-22"

# タイムラインをクライアントに送信する直前のイベントハンドラ
def OnPreSendMessageTimelineStatus(sender, e):
	e.Text = re_a.sub(lambda m: re_re.sub("/"+affiliate_tag, m.group()), e.Text)

# 後片付けイベントハンドラ(これを行わないとイベントが外れないのでリロードするたびに増えてしまう)
def OnBeforeUnload(sender, e):
	Session.PreSendMessageTimelineStatus -= OnPreSendMessageTimelineStatus

# イベントハンドラを接続
Session.PreSendMessageTimelineStatus += OnPreSendMessageTimelineStatus
Session.AddInManager.GetAddIn[DLRIntegrationAddIn]().BeforeUnload += OnBeforeUnload