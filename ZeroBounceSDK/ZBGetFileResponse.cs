namespace ZeroBounceSDK
{
    public class ZBGetFileResponse : ZBResponse
    {
        public string LocalFilePath;

        public override string ToString()
        {
            return "ZBGetFileResponse{localFilePath='" + LocalFilePath + "'}";
        }
    }
}