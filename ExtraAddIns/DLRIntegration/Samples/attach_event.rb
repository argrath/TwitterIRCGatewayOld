include System

on_pre_send_message_timeline_status = Proc.new do |sender, e|
	e.text = "#{e.text} (by #{e.status.user.name})"
end

Session.pre_send_message_timeline_status(&on_pre_send_message_timeline_status)