using Newtonsoft.Json;

namespace ZeroBounceSDK
{
    public class ZBGetFileResponse : ZBResponse
    {
        [JsonProperty("success")]
        public bool Success { get; set; } = true;

        [JsonProperty("message")]
        public string Message { get; set; }

        [JsonProperty("error")]
        public string Error { get; set; }

        [JsonProperty("local_file_path")]
        public string LocalFilePath { get; set; }

        public override string ToString()
        {
            return "ZBGetFileResponse{localFilePath='" + LocalFilePath + "', success=" + Success + "}";
        }
    }
}