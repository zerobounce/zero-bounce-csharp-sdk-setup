using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using Newtonsoft.Json;

namespace ZeroBounceSDK
{
    public sealed class ZeroBounce
    {
        private static readonly ZeroBounce _instance = new ZeroBounce();

        // Explicit static constructor to tell C# compiler
        // not to mark type as beforefieldinit
        static ZeroBounce()
        {
        }

        private ZeroBounce()
        {
        }

        public static ZeroBounce Instance => _instance;

        private const string ApiBaseUrl = "https://api.zerobounce.net/v2";
        private const string BulkApiBaseUrl = "https://bulkapi.zerobounce.net/v2";
        private readonly HttpClient _client = new HttpClient();
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
                failureCallback);
        }

        /// <sumary>This API will tell you how many credits you have left on your account. It's simple, fast and easy to use.</sumary>
        /// <param name="successCallback"> The success callback function, called with a ZBCreditsResponse object</param>
        /// <param name="failureCallback"> The failure callback function, called with a string error message</param>
        public void GetCredits(Action<ZBCreditsResponse> successCallback, Action<string> failureCallback)
        {
            if (InvalidApiKey(failureCallback)) return;

            _sendRequest(ApiBaseUrl + "/getcredits?api_key=" + _apiKey,
                successCallback, failureCallback);
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
                failureCallback);
        }

        /// <param name="fileId">The returned file ID when calling sendFile API.</param>
        /// <param name="successCallback"> The success callback function, called with a ZBFileStatusResponse object</param>
        /// <param name="failureCallback"> The failure callback function, called with a string error message</param>
        public void FileStatus(string fileId,
            Action<ZBFileStatusResponse> successCallback, Action<string> failureCallback)
        {
            if (InvalidApiKey(failureCallback)) return;

            _sendRequest(
                BulkApiBaseUrl + "/filestatus?api_key=" + _apiKey + "&file_id=" + fileId,
                successCallback, failureCallback);
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
        /// The sendfile API allows user to send a file for bulk email validation
        /// </summary>
        public async void SendFile(string filePath, int emailAddressColumn, SendFileOptions options,
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

                const string url = BulkApiBaseUrl + "/sendFile";
                var result = await _client.PostAsync(url, content);
                var responseString = await result.Content.ReadAsStringAsync();
                var response = JsonConvert.DeserializeObject<ZBSendFileResponse>(responseString);
                successCallback(response);
            }
            catch (Exception e)
            {
                failureCallback(e.Message);
            }
        }

        /// <summary>
        /// The getfile API allows users to get the validation results file for the file been submitted using sendfile API
        /// </summary>
        public async void GetFile(string fileId, string localDownloadPath, 
            Action<ZBGetFileResponse> successCallback, Action<string> failureCallback)
        {
            if (InvalidApiKey(failureCallback)) return;

            try
            {
                var url = BulkApiBaseUrl + "/getFile?api_key=" + _apiKey + "&file_id=" + fileId;
                var stream = await _client.GetStreamAsync(url);
               
                var dirPath = Path.GetDirectoryName(localDownloadPath);
                if(dirPath != null) Directory.CreateDirectory(dirPath);
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

        /// <param name="fileId">The returned file ID when calling sendfile API.</param>
        public void DeleteFile(string fileId, Action<ZBDeleteFileResponse> successCallback, Action<string> failureCallback) {
            if (InvalidApiKey(failureCallback)) return;

            _sendRequest(
                BulkApiBaseUrl + "/deletefile?api_key=" + _apiKey + "&file_id=" + fileId,
                successCallback,
                failureCallback);
        }
        
        private bool InvalidApiKey(Action<string> failureCallback)
        {
            if (_apiKey != null) return false;
            failureCallback("ZeroBounce SDK is not initialized. " +
                            "Please call ZeroBounceSDK.initialize(context, apiKey) first");
            return true;
        }

        private async void _sendRequest<T>(string url, Action<T> successCallback, Action<string> failureCallback)
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