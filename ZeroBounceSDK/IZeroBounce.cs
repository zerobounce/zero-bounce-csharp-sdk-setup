// The public API of ZeroBounce. Keep it in step with the class: the
// InterfaceDriftTests fail when a public instance member is on one and not the other.

using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json.Linq;

namespace ZeroBounceSDK
{
    /// <summary>
    /// The public API of the ZeroBounce validation client: every API call on
    /// <see cref="ZeroBounce"/>, so code that takes an <see cref="IZeroBounce"/>
    /// can be given a mock in tests (Moq, NSubstitute or a fake of your own).
    /// <para>
    /// Setup is not part of it: <see cref="ZeroBounce.Instance"/>, the
    /// <c>Initialize</c> overloads and the base-URL setters stay on
    /// <see cref="ZeroBounce"/>, so code that receives a client never handles the key.
    /// </para>
    /// <para>
    /// Meant to be mocked, not implemented: new API methods are added here in
    /// minor releases, as they are on <see cref="ZeroBounce"/>, and a class of
    /// your own that implements it would then stop compiling.
    /// </para>
    /// </summary>
    public interface IZeroBounce
    {
        /// <summary>
        /// The outcome of the most recent call on this client: null when it
        /// succeeded, otherwise the short message its failure callback was
        /// given plus the HTTP status (0 when the failure never reached the
        /// API: a rejected argument, a missing file, or a transport error).
        /// <para>
        /// It is set synchronously, immediately before either callback runs,
        /// so reading it from inside the failure callback always describes the
        /// call that is failing, and a later success clears it. Use one client
        /// per key and per thread, as the rest of this SDK already asks.
        /// </para>
        /// </summary>
        ZBApiError LastApiError { get; }

        /// <param name="email">The email address you want to validate</param>
        /// <param name="ipAddress"> The IP Address the email signed up from (Can be blank).</param>
        /// <param name="successCallback"> The success callback function, called with a ZBValidateResponse object</param>
        /// <param name="failureCallback"> The failure callback function, called with a string error message</param>
        void Validate(string email, string ipAddress, Action<ZBValidateResponse> successCallback,
            Action<string> failureCallback);

        void Validate(string email, string ipAddress, ZBValidateOptions options,
            Action<ZBValidateResponse> successCallback, Action<string> failureCallback);

        /// <param name="emailBatch">A list of ZBValidateEmailRow objects</param>
        /// <param name="successCallback"> The success callback function, called with a ZBValidateResponse object</param>
        /// <param name="failureCallback"> The failure callback function, called with a string error message</param>
        void ValidateBatch(List<ZBValidateEmailRow> emailBatch, Action<ZBValidateBatchResponse> successCallback,
            Action<string> failureCallback);

        /// <summary>
        /// Validates a batch of addresses in one request (POST /validatebatch).
        /// <para>
        /// Of <see cref="ZBValidateOptions"/> the batch sends only
        /// <see cref="ZBValidateOptions.Timeout"/> (10-120 seconds),
        /// <see cref="ZBValidateOptions.ActivityData"/> and
        /// <see cref="ZBValidateOptions.VerifyPlus"/>.
        /// <see cref="ZBValidateOptions.UsePost"/>,
        /// <see cref="ZBValidateOptions.DomainInfo"/>,
        /// <see cref="ZBValidateOptions.GreylistProcessing"/> and
        /// <see cref="ZBValidateOptions.CallbackUrl"/> apply to
        /// <see cref="Validate(string, string, ZBValidateOptions, Action{ZBValidateResponse}, Action{string})"/>
        /// only; the batch ignores them and does not check them.
        /// </para>
        /// </summary>
        void ValidateBatch(List<ZBValidateEmailRow> emailBatch, ZBValidateOptions options,
            Action<ZBValidateBatchResponse> successCallback, Action<string> failureCallback);

        /// <summary>This API will tell you how many credits you have left on your account. It's simple, fast and easy to use.</summary>
        /// <param name="successCallback"> The success callback function, called with a ZBCreditsResponse object</param>
        /// <param name="failureCallback"> The failure callback function, called with a string error message</param>
        void GetCredits(Action<ZBCreditsResponse> successCallback, Action<string> failureCallback);

        /// <param name="startDate">The start date of when you want to view API usage</param>
        /// <param name="endDate">The end date of when you want to view API usage</param>
        void GetApiUsage(
            DateTime startDate, DateTime endDate,
            Action<ZBGetApiUsageResponse> successCallback,
            Action<string> failureCallback);

        /// <param name="email">The email address for which we are interested in getting the engagement</param>
        void GetActivity(string email, Action<ZBActivityResponse> successCallback,
            Action<string> failureCallback);

        /// <summary>Real-time AI scoring for a single email (GET /scoring).</summary>
        void ScoreEmail(string email, Action<ZBScoreEmailResponse> successCallback,
            Action<string> failureCallback);

        /// <summary>Real-time AI scoring for a single email (POST /scoring, form-encoded).</summary>
        void ScoreEmailPost(string email, Action<ZBScoreEmailResponse> successCallback,
            Action<string> failureCallback);

        /// <param name="domain">The email domain for which to find the email format.</param>
        /// <param name="firstName">The first name of the person whose email format is being searched.</param>
        /// <param name="middleName">The middle name of the person whose email format is being searched. [optional]</param>
        /// <param name="lastName">The last name of the person whose email format is being searched. [optional]</param>
        void FindEmailByDomain(
            string domain, string firstName, string middleName, string lastName,
            Action<ZBEmailFinderResponse> successCallback,
            Action<string> failureCallback);

        /// <param name="domain">The email domain for which to find the email format.</param>
        /// <param name="firstName">The first name of the person whose email format is being searched.</param>
        /// <param name="lastName">The last name of the person whose email format is being searched. [optional]</param>
        void FindEmailByDomain(
            string domain, string firstName, string lastName,
            Action<ZBEmailFinderResponse> successCallback,
            Action<string> failureCallback);

        /// <param name="domain">The email domain for which to find the email format.</param>
        /// <param name="firstName">The first name of the person whose email format is being searched.</param>
        void FindEmailByDomain(
            string domain, string firstName,
            Action<ZBEmailFinderResponse> successCallback,
            Action<string> failureCallback);

        /// <param name="companyName">The company name for which to find the email format.</param>
        /// <param name="firstName">The first name of the person whose email format is being searched.</param>
        /// <param name="middleName">The middle name of the person whose email format is being searched. [optional]</param>
        /// <param name="lastName">The last name of the person whose email format is being searched. [optional]</param>
        void FindEmailByCompanyName(
            string companyName, string firstName, string middleName, string lastName,
            Action<ZBEmailFinderResponse> successCallback,
            Action<string> failureCallback);

        /// <param name="companyName">The company name for which to find the email format.</param>
        /// <param name="firstName">The first name of the person whose email format is being searched.</param>
        /// <param name="lastName">The last name of the person whose email format is being searched. [optional]</param>
        void FindEmailByCompanyName(
            string companyName, string firstName, string lastName,
            Action<ZBEmailFinderResponse> successCallback,
            Action<string> failureCallback);

        /// <param name="companyName">The company name for which to find the email format.</param>
        /// <param name="firstName">The first name of the person whose email format is being searched.</param>
        void FindEmailByCompanyName(
            string companyName, string firstName,
            Action<ZBEmailFinderResponse> successCallback,
            Action<string> failureCallback);

        /// <param name="domain">The email domain for which to find the email format.</param>
        void FindDomainByDomain(
            string domain,
            Action<ZBDomainSearchResponse> successCallback,
            Action<string> failureCallback);

        /// <param name="companyName">The company name for which to find the email format</param>
        void FindDomainByCompanyName(
            string companyName,
            Action<ZBDomainSearchResponse> successCallback,
            Action<string> failureCallback);

        /// <param name="fileId">The returned file ID when calling sendFile API.</param>
        /// <param name="successCallback"> The success callback function, called with a ZBFileStatusResponse object</param>
        /// <param name="failureCallback"> The failure callback function, called with a string error message</param>
        void FileStatus(string fileId,
            Action<ZBFileStatusResponse> successCallback, Action<string> failureCallback);

        /// <param name="fileId">The returned file ID when calling scoringSendFile API.</param>
        /// <param name="successCallback"> The success callback function, called with a ZBFileStatusResponse object</param>
        /// <param name="failureCallback"> The failure callback function, called with a string error message</param>
        void ScoringFileStatus(string fileId,
            Action<ZBFileStatusResponse> successCallback, Action<string> failureCallback);

        /// <summary>
        /// The scoringSendfile API allows user to send a file for bulk email validation
        /// <param name="successCallback"> The success callback function, called with a ZBSendFileResponse object</param>
        /// <param name="failureCallback"> The failure callback function, called with a string error message</param>
        /// </summary>
        void ScoringSendFile(string filePath, ZeroBounce.SendFileOptions options,
            Action<ZBSendFileResponse> successCallback, Action<string> failureCallback);

        /// <summary>
        /// The scoringSendfile API allows user to send a file for bulk email validation (from a stream).
        /// <param name="successCallback"> The success callback function, called with a ZBSendFileResponse object</param>
        /// <param name="failureCallback"> The failure callback function, called with a string error message</param>
        /// </summary>
        void ScoringSendFile(Stream file, string fileName, ZeroBounce.SendFileOptions options,
            Action<ZBSendFileResponse> successCallback, Action<string> failureCallback);

        /// <summary>
        /// The sendfile API allows user to send a file for bulk email validation
        /// <param name="successCallback"> The success callback function, called with a ZBSendFileResponse object</param>
        /// <param name="failureCallback"> The failure callback function, called with a string error message</param>
        /// </summary>
        void SendFile(string filePath, ZeroBounce.SendFileOptions options,
            Action<ZBSendFileResponse> successCallback, Action<string> failureCallback);

        /// <summary>
        /// The sendfile API allows user to send a file for bulk email validation (from a stream).
        /// <param name="successCallback"> The success callback function, called with a ZBSendFileResponse object</param>
        /// <param name="failureCallback"> The failure callback function, called with a string error message</param>
        /// </summary>
        void SendFile(Stream file, string fileName, ZeroBounce.SendFileOptions options,
            Action<ZBSendFileResponse> successCallback, Action<string> failureCallback);

        /// <summary>
        /// The scoringGetFile API allows users to get the validation results file for the file been submitted using scoringSendfile API
        /// <param name="successCallback"> The success callback function, called with a ZBGetFileResponse object</param>
        /// <param name="failureCallback"> The failure callback function, called with a string error message</param>
        /// </summary>
        void ScoringGetFile(string fileId, string localDownloadPath,
            Action<ZBGetFileResponse> successCallback, Action<string> failureCallback);

        /// <summary>
        /// Scoring getfile with optional <see cref="ZeroBounce.GetFileOptions.DownloadType"/> (<c>activity_data</c> is ignored).
        /// </summary>
        void ScoringGetFile(string fileId, string localDownloadPath, ZeroBounce.GetFileOptions getFileOptions,
            Action<ZBGetFileResponse> successCallback, Action<string> failureCallback);

        /// <summary>
        /// The getfile API allows users to get the validation results file for the file been submitted using sendfile API
        /// <param name="successCallback"> The success callback function, called with a ZBGetFileResponse object</param>
        /// <param name="failureCallback"> The failure callback function, called with a string error message</param>
        /// </summary>
        void GetFile(string fileId, string localDownloadPath,
            Action<ZBGetFileResponse> successCallback, Action<string> failureCallback);

        /// <summary>
        /// Validation getfile with optional <c>download_type</c> and <c>activity_data</c> query parameters.
        /// </summary>
        void GetFile(string fileId, string localDownloadPath, ZeroBounce.GetFileOptions getFileOptions,
            Action<ZBGetFileResponse> successCallback, Action<string> failureCallback);

        /// <param name="fileId">The returned file ID when calling scoringSendfile API.</param>
        /// <param name="successCallback"> The success callback function, called with a ZBDeleteFileResponse object</param>
        /// <param name="failureCallback"> The failure callback function, called with a string error message</param>
        void ScoringDeleteFile(string fileId, Action<ZBDeleteFileResponse> successCallback, Action<string> failureCallback);

        /// <param name="fileId">The returned file ID when calling sendfile API.</param>
        /// <param name="successCallback"> The success callback function, called with a ZBDeleteFileResponse object</param>
        /// <param name="failureCallback"> The failure callback function, called with a string error message</param>
        void DeleteFile(string fileId, Action<ZBDeleteFileResponse> successCallback, Action<string> failureCallback);

        void SendEmailFinderFile(string filePath, int domainColumn, ZeroBounce.EmailFinderSendFileOptions options,
            Action<ZBSendFileResponse> successCallback, Action<string> failureCallback);

        /// <summary>
        /// Queues the bulk email finder on an uploaded file (POST /email-finder/sendfile).
        /// <para>
        /// Besides domain_column the endpoint needs a name to work from, so
        /// either first_name_column or full_name_column must be set. Neither is
        /// checked by the server until the job runs, so the SDK rejects the
        /// call before the upload.
        /// </para>
        /// </summary>
        void SendEmailFinderFile(Stream file, string fileName, int domainColumn, ZeroBounce.EmailFinderSendFileOptions options,
            Action<ZBSendFileResponse> successCallback, Action<string> failureCallback);

        void EmailFinderFileStatus(string fileId,
            Action<ZBFileStatusResponse> successCallback, Action<string> failureCallback);

        void EmailFinderGetFile(string fileId, string localDownloadPath,
            Action<ZBGetFileResponse> successCallback, Action<string> failureCallback);

        void EmailFinderDeleteFile(string fileId,
            Action<ZBDeleteFileResponse> successCallback, Action<string> failureCallback);

        void SendDomainSearchFile(string filePath, int domainColumn, ZeroBounce.DomainSearchSendFileOptions options,
            Action<ZBSendFileResponse> successCallback, Action<string> failureCallback);

        void SendDomainSearchFile(Stream file, string fileName, int domainColumn, ZeroBounce.DomainSearchSendFileOptions options,
            Action<ZBSendFileResponse> successCallback, Action<string> failureCallback);

        void DomainSearchFileStatus(string fileId,
            Action<ZBFileStatusResponse> successCallback, Action<string> failureCallback);

        void DomainSearchGetFile(string fileId, string localDownloadPath,
            Action<ZBGetFileResponse> successCallback, Action<string> failureCallback);

        void DomainSearchDeleteFile(string fileId,
            Action<ZBDeleteFileResponse> successCallback, Action<string> failureCallback);

        void SendListEvaluatorFile(string filePath, int emailAddressColumn,
            Action<ZBSendFileResponse> successCallback, Action<string> failureCallback);

        void SendListEvaluatorFile(Stream file, string fileName, int emailAddressColumn,
            Action<ZBSendFileResponse> successCallback, Action<string> failureCallback);

        void GetListEvaluatorResult(string fileId, Action<JObject> successCallback, Action<string> failureCallback);

        /// <summary>
        /// Submits a finished evaluation for validation (POST /v2/send-evaluated-file/).
        /// <para>
        /// LIVE: this takes the list evaluator's file_id as a form field, not
        /// the file upload the docs describe - an upload is rejected with
        /// "file_id: This field is required". It returns a new validation
        /// job's file_id, which answers "File cannot be found" for a short
        /// while after it is accepted.
        /// </para>
        /// </summary>
        /// <param name="fileId">the file_id returned by the list evaluator</param>
        void SendEvaluatedFile(string fileId,
            Action<ZBSendFileResponse> successCallback, Action<string> failureCallback);

        void AddFilter(string rule, string target, string value,
            Action<JObject> successCallback, Action<string> failureCallback);

        void ListFilters(bool usePost, Action<JToken> successCallback, Action<string> failureCallback);

        void DeleteFilter(string rule, string target, string value,
            Action<JObject> successCallback, Action<string> failureCallback);

        void ListS3Files(string bucket, string search,
            Action<List<ZBS3ListFileEntry>> successCallback, Action<string> failureCallback);

        /// <summary>
        /// Validates a file in your Amazon S3 bucket (POST /validateS3file).
        /// <para>
        /// LIVE: return_url is required, as it is on every other S3 and SFTP
        /// job. Without it the API answers HTTP 400 "Provided return URL is not
        /// permitted: None", so the SDK rejects the call before sending it.
        /// </para>
        /// </summary>
        void ValidateS3File(ZeroBounce.ValidateS3FileOptions options,
            Action<ZBSendFileResponse> successCallback, Action<string> failureCallback);

        /// <summary>
        /// Scores a file in your Amazon S3 bucket (POST /scoreS3file).
        /// <para>
        /// LIVE: return_url is required here too. It was never sent at all
        /// before, which made this call impossible to complete.
        /// </para>
        /// </summary>
        void ScoreS3File(ZeroBounce.ScoreS3FileOptions options,
            Action<ZBSendFileResponse> successCallback, Action<string> failureCallback);

        void ExportS3File(string fileId, string exportBucket,
            Action<ZBSendFileResponse> successCallback, Action<string> failureCallback);

        /// <summary>Runs the email finder on a file in your Amazon S3 bucket (POST /emailFinderS3file).</summary>
        void EmailFinderS3File(ZeroBounce.BulkFileJobOptions options,
            Action<ZBSendFileResponse> successCallback, Action<string> failureCallback);

        /// <summary>Runs domain search on a file in your Amazon S3 bucket (POST /domainSearchS3file).</summary>
        void DomainSearchS3File(ZeroBounce.BulkFileJobOptions options,
            Action<ZBSendFileResponse> successCallback, Action<string> failureCallback);

        /// <summary>
        /// Validates a file from your SFTP/FTP server (POST /validateSFTPfile).
        /// <para>
        /// LIVE: file_name is required here too, although the docs list it as
        /// optional for this one endpoint.
        /// </para>
        /// </summary>
        void ValidateSFTPFile(ZeroBounce.BulkFileJobOptions options,
            Action<ZBSendFileResponse> successCallback, Action<string> failureCallback);

        /// <summary>Scores a file from your SFTP/FTP server (POST /scoreSFTPfile).</summary>
        void ScoreSFTPFile(ZeroBounce.BulkFileJobOptions options,
            Action<ZBSendFileResponse> successCallback, Action<string> failureCallback);

        /// <summary>Runs the email finder on a file from your SFTP/FTP server (POST /emailFinderSFTPfile).</summary>
        void EmailFinderSFTPFile(ZeroBounce.BulkFileJobOptions options,
            Action<ZBSendFileResponse> successCallback, Action<string> failureCallback);

        /// <summary>Runs domain search on a file from your SFTP/FTP server (POST /domainSearchSFTPfile).</summary>
        void DomainSearchSFTPFile(ZeroBounce.BulkFileJobOptions options,
            Action<ZBSendFileResponse> successCallback, Action<string> failureCallback);

        /// <summary>
        /// Exports a validated or scored file back to your SFTP/FTP server
        /// (POST /exportSFTPfile).
        /// <para>
        /// Note the field name: this endpoint takes export_path, not the
        /// export_bucket that <see cref="ExportS3File"/> sends. Pass null to
        /// use the export path from your ZeroBounce settings.
        /// </para>
        /// </summary>
        void ExportSFTPFile(string fileId, string exportPath,
            Action<ZBSendFileResponse> successCallback, Action<string> failureCallback);

        /// <summary>Seed providers available for new tests (GET /api/ipt/providers).</summary>
        void GetInboxPlacementProviders(Action<JObject> successCallback, Action<string> failureCallback);

        /// <summary>Supported test types and the default one (GET /api/ipt/test-types).</summary>
        void GetInboxPlacementTestTypes(Action<JObject> successCallback, Action<string> failureCallback);

        /// <summary>
        /// Starts a test and returns its seed list (POST /api/ipt/test).
        /// <para>
        /// The name is at most 64 characters, the test type is 1, 2 or 3, and
        /// type 1 (send your own campaign to the seeds) needs the address you
        /// will send from, so senderEmail is required with it.
        /// </para>
        /// </summary>
        void CreateInboxPlacementTest(string name, ZeroBounce.CreateInboxPlacementTestOptions options,
            Action<JObject> successCallback, Action<string> failureCallback);

        /// <summary>The results of one test (GET /api/ipt/test/{id}).</summary>
        void GetInboxPlacementTest(string testId, Action<JObject> successCallback, Action<string> failureCallback);

        /// <summary>The account's tests (GET /api/ipt/tests).</summary>
        void ListInboxPlacementTests(ZeroBounce.ListInboxPlacementTestsOptions options,
            Action<JObject> successCallback, Action<string> failureCallback);

        /// <summary>Deletes one test (DELETE /api/ipt/test/{id}).</summary>
        void DeleteInboxPlacementTest(string testId, Action<JObject> successCallback, Action<string> failureCallback);

        /// <summary>The configured webhook (GET /api/ipt/webhook).</summary>
        void GetInboxPlacementWebhook(Action<JObject> successCallback, Action<string> failureCallback);

        /// <summary>Sets the webhook that test results post to (PUT /api/ipt/webhook).</summary>
        void SetInboxPlacementWebhook(string webhookUrl, Action<JObject> successCallback, Action<string> failureCallback);
    }
}
