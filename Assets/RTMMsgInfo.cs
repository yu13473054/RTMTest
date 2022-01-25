namespace PVP.Game
{
    public class RTMMsgInfo
    {
        public int type;
        public long typeValue;
        public long messageId;
        public long fromUid;
        public byte messageType;
        public string stringMessage = null;
        public byte[] binaryMessage = null;
        public byte[] attrs;
        // public long modifiedTime;
        public string fileUrl;          //-- File url
        public int filesize = 0;        //-- File size
        // -- For image type
        // public string surl;         //-- Thumb url, only for image type.

        //-- For RTM audio
        public bool isRTMAudio = false;
        public string language;
        public int duration = 0;
        public byte[] audioData = null;
    }
}
