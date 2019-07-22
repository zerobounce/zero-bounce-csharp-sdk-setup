using System.Collections.Generic;
using Newtonsoft.Json;

namespace ZeroBounceSDK
{
    public class ZBDeleteFileResponse : ZBResponse
    {
        [JsonProperty("success")] public bool Success;

        [JsonProperty("message")] [JsonConverter(typeof(SingleOrArrayConverter<string>))]
        public List<string> Message;

        [JsonProperty("file_name")] public string FileName;
        [JsonProperty("file_id")] public string FileId;

        public override string ToString()
        {
            return "ZBSendFileResponse{" +
                   "success=" + Success +
                   ", message='" + string.Join(", ", Message)+ '\'' +
                   ", fileName='" + FileName + '\'' +
                   ", fileId='" + FileId + '\'' +
                   '}';
        }
    }
}