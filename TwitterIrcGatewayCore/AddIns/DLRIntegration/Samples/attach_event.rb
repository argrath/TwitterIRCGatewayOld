# ステータス更新直前のイベント
Session.pre_send_update_status do |sender, e|
	# CLR String -> Ruby String に変換する必要があるっぽい
	if e.text.to_s.include?("はうはう")
		Session.send_server(Misuzilla::Net::Irc::NoticeMessage.new(e.received_message.receiver, "はうはう税を徴収する!それまでははうはうさせない!"))

		# キャンセルすることで送信させない
		e.cancel = true
	end
end

# タイムラインの一ステータスを受信してクライアントに送信する直前のイベント
Session.pre_send_message_timeline_status do |sender, e|
	e.text = "#{e.text} (by #{e.status.user.name})"
end

# IRCメッセージを受け取ったときのイベント
Session.message_received do |sender, e|
	if e.message.command.to_s == "HAUHAU"
		Session.send_server_error_message("Hauhau!")
		e.cancel = true
	end
end