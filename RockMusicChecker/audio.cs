using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Serialization;

namespace RockMusicChecker
{
    public class audio
    {
        [XmlElement("type")]
        public string type { get; set; }

        [XmlElement("title")]
        public string title { get; set; }

        [XmlElement("artist")]
        public string artist { get; set; }
    }
}
