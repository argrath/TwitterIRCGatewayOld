import clr
clr.AddReferenceByPartialName("System.Windows.Forms")
from System.Windows.Forms import *

def OnPreSendMessageTimelineStatus(sender, e):
	e.Text = e.Text + " (by "+ e.Status.User.Name +")"

Session.PreSendMessageTimelineStatus += OnPreSendMessageTimelineStatus
