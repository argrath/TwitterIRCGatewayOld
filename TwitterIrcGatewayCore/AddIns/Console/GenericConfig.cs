using System;
using System.Collections.Generic;
using System.Text;

namespace Misuzilla.Applications.TwitterIrcGateway.AddIns.Console
{
    public class GeneralConfig : IConfiguration
    {
        public Int32 SearchCount;
        public Int32 TimelineCount;
        public Int32 FavoritesCount;
        public Boolean ShowPermalinkAfterStatus;

        public GeneralConfig()
        {
            SearchCount = 10;
            TimelineCount = 10;
            FavoritesCount = 10;
            ShowPermalinkAfterStatus = false;
        }
    }
}
