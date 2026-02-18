using Newtonsoft.Json;

namespace ZeroBounceSDK
{
    public class ZBFileStatusResponse : ZBResponse
    {
        [JsonProperty("success")] public bool Success;
        [JsonProperty("message")] public string Message;
        [JsonProperty("file_status")] public string FileStatus;
        [JsonProperty("file_id")] public string FileId;
        [JsonProperty("file_name")] public string FileName;
        [JsonProperty("upload_date")] public string UploadDate;
        [JsonProperty("complete_percentage")] public string CompletePercentage;
        [JsonProperty("error_reason")] public string ErrorReason;
        [JsonProperty("return_url")] public string ReturnUrl;

        public override string ToString()
        {
            return "ZBFileStatusResponse{" +
                   "success=" + Success +
                   ", message='" + Message + "'" +
                   ", fileStatus='" + FileStatus + "'" +
                   ", fileId='" + FileId + "'" +
                   ", fileName='" + FileName + "'" +
                   ", uploadDate='" + UploadDate + "'" +
                   ", completePercentage='" + CompletePercentage + "'" +
                   ", errorReason='" + ErrorReason + "'" +
                   ", returnUrl='" + ReturnUrl + "'" +
                   '}';
        }
    }
}