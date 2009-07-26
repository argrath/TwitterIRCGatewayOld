using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Misuzilla.Net.Irc;

namespace Misuzilla.Applications.TwitterIrcGateway
{
    /// <summary>
    /// 認証結果を保持します。
    /// </summary>
    public class AuthenticateResult : MarshalByRefObject
    {
        /// <summary>
        /// ユーザのアクセスが許可されているかどうかを取得・設定します。
        /// </summary>
        public Boolean IsAuthenticated { get; set; }

        /// <summary>
        /// 認証が失敗した理由のリプライを返します。
        /// </summary>
        public ErrorReply ErrorReply { get; set; }

        /// <summary>
        /// 認証が失敗した理由を返します。
        /// </summary>
        public String ErrorMessage { get; set; }

        public AuthenticateResult()
        {
            IsAuthenticated = true;
        }

        public AuthenticateResult(ErrorReply errorReply, String message)
        {
            IsAuthenticated = false;
            ErrorReply = errorReply;
            ErrorMessage = message;
        }
    }

    public class TwitterAuthenticateResult : AuthenticateResult
    {
        public User User { get; set; }
        public TwitterAuthenticateResult(User user) : base()
        {
            User = user;
        }
    }
}
