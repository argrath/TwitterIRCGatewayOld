using System;
using System.Collections.Generic;
using System.Text;

namespace Misuzilla.Applications.TwitterIrcGateway.AddIns.Console
{
    public class GeneralConfig : IConfiguration
    {
        public Int32 SearchCount;
        public Int32 TimelineCount;
        public Boolean ShowPermalinkAfterStatus;

        public GeneralConfig()
        {
            SearchCount = 10;
            TimelineCount = 10;
            ShowPermalinkAfterStatus = false;
        }
    }
}
