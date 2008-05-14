// $Id$
using System;
using System.IO;
using System.Net;

namespace Misuzilla.Net.Irc
{
	public class IRCException : ApplicationException
	{
		public IRCException(String message) : base(message) {}
	}
	public class IRCNotConnectedException : IRCException
	{
		public IRCNotConnectedException(String message) : base(message) {}
	}
	public class IRCInvalidMessageException : IRCException
	{
		public IRCInvalidMessageException(String message)
			: base("メッセージの形式が不正です\nメッセージ: " + message) {}
	}
}
