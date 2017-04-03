using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Serialization;

namespace RockMusicChecker
{
    [XmlRoot("nexgen_audio_export")]

    public class nexgen_audio_export
    {
        [XmlElement("audio")]
        public List<audio> audio { get; set; }
    }
}
