using System;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace ZeroBounceSDK
{
    public class ZeroBounce
    {
        protected static readonly ZeroBounce _instance = new ZeroBounce();

        // Explicit static constructor to tell C# compiler
        // not to mark type as beforefieldinit
        static ZeroBounce()
        {
        }
        protected ZeroBounce()
        {
        }
        public static ZeroBounce Instance => _instance;

        private const string ApiBaseUrl = "https://api.zerobounce.net/v2";
        private const string BulkApiBaseUrl = "https://bulkapi.zerobounce.net/v2";
        protected HttpClient _client = new HttpClient();
        private string _apiKey;

        public void Initialize(string apiKey)
        {
            _apiKey = apiKey;
        }

        /// <param name="email">The email address you want to validate</param>
        /// <param name="ipAddress"> The IP Address the email signed up from (Can be blank).</param>
        /// <param name="successCallback"> The success callback function, called with a ZBValidateResponse object</param>
        /// <param name="failureCallback"> The failure callback function, called with a string error message</param>
        public void Validate(string email, string ipAddress, Action<ZBValidateResponse> successCallback,
            Action<string> failureCallback)
        {
            if (InvalidApiKey(failureCallback)) return;

            _sendRequest(
                ApiBaseUrl + "/validate?api_key=" + _apiKey + "&email=" + email + "&ip_address=" + (ipAddress ?? ""),
                successCallback,
                failureCallback).Wait(); 
        }

        /// <sumary>This API will tell you how many credits you have left on your account. It's simple, fast and easy to use.</sumary>
        /// <param name="successCallback"> The success callback function, called with a ZBCreditsResponse object</param>
        /// <param name="failureCallback"> The failure callback function, called with a string error message</param>
        public void GetCredits(Action<ZBCreditsResponse> successCallback, Action<string> failureCallback)
        {
            if (InvalidApiKey(failureCallback)) return;

            _sendRequest(ApiBaseUrl + "/getcredits?api_key=" + _apiKey,
                successCallback, failureCallback).Wait();
        }

        /// <param name="startDate">The start date of when you want to view API usage</param>
        /// <param name="endDate">The end date of when you want to view API usage</param>
        public void GetApiUsage(
            DateTime startDate, DateTime endDate,
            Action<ZBGetApiUsageResponse> successCallback,
            Action<string> failureCallback)
        {
            if (InvalidApiKey(failureCallback)) return;

            _sendRequest(
                ApiBaseUrl + "/getapiusage?api_key=" + _apiKey
                + "&start_date=" + startDate.ToString("yyyy-MM-dd")  
                + "&end_date=" + endDate.ToString("yyyy-MM-dd"),
                successCallback,
                failureCallback).Wait();
        }

        /// <param name="email">The email address for which we are interested in getting the engagement</param>
        public void GetActivity(string email, Action<ZBActivityResponse> successCallback,
            Action<string> failureCallback)
        {
            if (InvalidApiKey(failureCallback)) return;

            _sendRequest(
                ApiBaseUrl + "/activity?api_key=" + _apiKey
                + "&email=" + email,
                successCallback,
                failureCallback).Wait();
        }

        /// <param name="fileId">The returned file ID when calling sendFile API.</param>
        /// <param name="successCallback"> The success callback function, called with a ZBFileStatusResponse object</param>
        /// <param name="failureCallback"> The failure callback function, called with a string error message</param>
        public void FileStatus(string fileId,
            Action<ZBFileStatusResponse> successCallback, Action<string> failureCallback)
        {
            _FileStatus(false, fileId, successCallback, failureCallback);
        }
        
        /// <param name="fileId">The returned file ID when calling scoringSendFile API.</param>
        /// <param name="successCallback"> The success callback function, called with a ZBFileStatusResponse object</param>
        /// <param name="failureCallback"> The failure callback function, called with a string error message</param>
        public void ScoringFileStatus(string fileId,
            Action<ZBFileStatusResponse> successCallback, Action<string> failureCallback)
        {
            _FileStatus(true, fileId, successCallback, failureCallback);
        }

        private void _FileStatus(bool scoring, string fileId,
            Action<ZBFileStatusResponse> successCallback, Action<string> failureCallback)
        {
            if (InvalidApiKey(failureCallback)) return;

            _sendRequest(
                BulkApiBaseUrl + (scoring ? "/scoring" : "") + "/filestatus?api_key=" + _apiKey + "&file_id=" + fileId,
                successCallback, failureCallback).Wait();
        }

        
        public class SendFileOptions
        {
            public string ReturnUrl;
            public int FirstNameColumn;
            public int LastNameColumn;
            public int GenderColumn;
            public int IpAddressColumn;
            public bool HasHeaderRow;
        }
        
        /// <summary>
        /// The scoringSendfile API allows user to send a file for bulk email validation
        /// <param name="successCallback"> The success callback function, called with a ZBSendFileResponse object</param>
        /// <param name="failureCallback"> The failure callback function, called with a string error message</param>
        /// </summary>
        public void ScoringSendFile(string filePath, int emailAddressColumn, string returnUrl, bool hasHeaderRow,
            Action<ZBSendFileResponse> successCallback, Action<string> failureCallback)
        {
            _SendFile(true, filePath, emailAddressColumn, new SendFileOptions
            {
                ReturnUrl = returnUrl,
                HasHeaderRow = hasHeaderRow,
            }, successCallback, failureCallback);
        }

        /// <summary>
        /// The sendfile API allows user to send a file for bulk email validation
        /// <param name="successCallback"> The success callback function, called with a ZBSendFileResponse object</param>
        /// <param name="failureCallback"> The failure callback function, called with a string error message</param>
        /// </summary>
        public void SendFile(string filePath, int emailAddressColumn, SendFileOptions options,
            Action<ZBSendFileResponse> successCallback, Action<string> failureCallback)
        {
            _SendFile(false, filePath, emailAddressColumn, options, successCallback, failureCallback);
        }

        private async void _SendFile(bool scoring, string filePath, int emailAddressColumn, SendFileOptions options,
            Action<ZBSendFileResponse> successCallback, Action<string> failureCallback)
        {
            if (InvalidApiKey(failureCallback)) return;
            
            try
            {
                var content = new MultipartFormDataContent();
                var file = File.OpenRead(filePath);
                content.Add(new StreamContent(file), "file", Path.GetFileName(filePath));

                content.Add(new StringContent(_apiKey), "api_key");
                content.Add(new StringContent(emailAddressColumn.ToString()), "email_address_column");

                if (options != null)
                {
                    if (options.ReturnUrl != null)
                        content.Add(new StringContent(options.ReturnUrl), "return_url");
                    if (options.HasHeaderRow)
                        content.Add(new StringContent("true"), "has_header_row");
                    if (options.FirstNameColumn > 0)
                        content.Add(new StringContent(options.FirstNameColumn.ToString()), "first_name_column");
                    if (options.LastNameColumn > 0)
                        content.Add(new StringContent(options.LastNameColumn.ToString()), "last_name_column");
                    if (options.GenderColumn > 0)
                        content.Add(new StringContent(options.GenderColumn.ToString()), "gender_column");
                    if (options.IpAddressColumn > 0)
                        content.Add(new StringContent(options.IpAddressColumn.ToString()), "ip_address_column");
                }

                var url = BulkApiBaseUrl + (scoring ? "/scoring" : "") + "/sendFile";
                var result = await _client.PostAsync(url, content);
                var responseString = await result.Content.ReadAsStringAsync();
                var response = JsonConvert.DeserializeObject<ZBSendFileResponse>(responseString);
                successCallback(response);
                file.Close();
            }
            catch (Exception e)
            {
                failureCallback(e.Message);
            }
        }

        /// <summary>
        /// The scoringGetFile API allows users to get the validation results file for the file been submitted using scoringSendfile API
        /// <param name="successCallback"> The success callback function, called with a ZBGetFileResponse object</param>
        /// <param name="failureCallback"> The failure callback function, called with a string error message</param>
        /// </summary>
        public void ScoringGetFile(string fileId, string localDownloadPath, 
            Action<ZBGetFileResponse> successCallback, Action<string> failureCallback)
        {
            _GetFile(true, fileId, localDownloadPath, successCallback, failureCallback);
        }
        
        /// <summary>
        /// The getfile API allows users to get the validation results file for the file been submitted using sendfile API
        /// <param name="successCallback"> The success callback function, called with a ZBGetFileResponse object</param>
        /// <param name="failureCallback"> The failure callback function, called with a string error message</param>
        /// </summary>
        public void GetFile(string fileId, string localDownloadPath, 
            Action<ZBGetFileResponse> successCallback, Action<string> failureCallback)
        {
            _GetFile(false, fileId, localDownloadPath, successCallback, failureCallback);
        }

        private async void _GetFile(bool scoring, string fileId, string localDownloadPath, 
            Action<ZBGetFileResponse> successCallback, Action<string> failureCallback)
        {
            if (InvalidApiKey(failureCallback)) return;

            try
            {
                var url = BulkApiBaseUrl + (scoring ? "/scoring" : "") + "/getFile?api_key=" + _apiKey + "&file_id=" + fileId;
                var stream = await _client.GetStreamAsync(url);
               
                var dirPath = Path.GetDirectoryName(localDownloadPath);
                if(dirPath != null & dirPath != "") Directory.CreateDirectory(dirPath);
                var fileStream = new FileStream(localDownloadPath, FileMode.Create, FileAccess.Write, FileShare.None, 4096,
                    true);
                await stream.CopyToAsync(fileStream);
                fileStream.Close();
                successCallback(new ZBGetFileResponse
                {
                    LocalFilePath = Path.GetFullPath(localDownloadPath)
                });
            }
            catch (Exception e)
            {
                failureCallback(e.Message);
            }
        }
        
        
        /// <param name="fileId">The returned file ID when calling scoringSendfile API.</param>
        /// <param name="successCallback"> The success callback function, called with a ZBDeleteFileResponse object</param>
        /// <param name="failureCallback"> The failure callback function, called with a string error message</param>
        public void ScoringDeleteFile(string fileId, Action<ZBDeleteFileResponse> successCallback, Action<string> failureCallback) {
            _DeleteFile(true, fileId, successCallback, failureCallback);
        }


        /// <param name="fileId">The returned file ID when calling sendfile API.</param>
        /// <param name="successCallback"> The success callback function, called with a ZBDeleteFileResponse object</param>
        /// <param name="failureCallback"> The failure callback function, called with a string error message</param>
        public void DeleteFile(string fileId, Action<ZBDeleteFileResponse> successCallback, Action<string> failureCallback) {
            _DeleteFile(false, fileId, successCallback, failureCallback);
        }

        private void _DeleteFile(bool scoring, string fileId, Action<ZBDeleteFileResponse> successCallback, Action<string> failureCallback) {
            if (InvalidApiKey(failureCallback)) return;

            _sendRequest(
                BulkApiBaseUrl + (scoring ? "/scoring" : "") + "/deletefile?api_key=" + _apiKey + "&file_id=" + fileId,
                successCallback,
                failureCallback).Wait();
        }

        private bool InvalidApiKey(Action<string> failureCallback)
        {
            if (_apiKey != null) return false;
            failureCallback("ZeroBounce SDK is not initialized. " +
                            "Please call ZeroBounce.Instance.Initialize(apiKey) first");
            return true;
        }

        private async Task _sendRequest<T>(string url, Action<T> successCallback, Action<string> failureCallback)
            where T : ZBResponse
        {
            try
            {
                var responseString = await _client.GetStringAsync(url);
                //Debug.WriteLine("sendRequest response: "+responseString);
                var response = JsonConvert.DeserializeObject<T>(responseString);
                successCallback(response);
            }
            catch (Exception e)
            {
                failureCallback(e.Message);
            }
        }
    }
}