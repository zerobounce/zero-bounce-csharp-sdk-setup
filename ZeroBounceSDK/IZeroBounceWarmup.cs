// The public API of ZeroBounceWarmup. Keep it in step with the class: the
// InterfaceDriftTests fail when a public instance member is on one and not the other.

using System;
using Newtonsoft.Json.Linq;

namespace ZeroBounceSDK
{
    /// <summary>
    /// The public API of the Warmup client: every API call on
    /// <see cref="ZeroBounceWarmup"/>, so code that takes an
    /// <see cref="IZeroBounceWarmup"/> can be given a mock in tests.
    /// <para>
    /// Setup is not part of it: the constructors and the <c>Initialize</c>
    /// overloads stay on <see cref="ZeroBounceWarmup"/>.
    /// </para>
    /// <para>
    /// Meant to be mocked, not implemented: new API methods are added here in
    /// minor releases, as they are on <see cref="ZeroBounceWarmup"/>, and a
    /// class of your own that implements it would then stop compiling.
    /// </para>
    /// </summary>
    public interface IZeroBounceWarmup
    {
        /// <summary>
        /// The outcome of the most recent call on this client: null when it
        /// succeeded, otherwise the short message its failure callback was
        /// given plus the HTTP status (0 when the failure never reached the
        /// API). It is set synchronously, immediately before either callback
        /// runs.
        /// </summary>
        ZBApiError LastApiError { get; }

        /// <summary>Every sending account of the key's team. GET /sending-accounts</summary>
        void ListSendingAccounts(Action<JToken> ok, Action<string> fail);

        /// <summary>One sending account. GET /sending-account</summary>
        void GetSendingAccount(string fromAddress, Action<JToken> ok, Action<string> fail);

        /// <summary>
        /// Adds a from-address to warmup. POST /sending-accounts
        /// <para>
        /// <c>self_managed</c> runs your own mailbox and needs
        /// <see cref="ZeroBounceWarmup.AddSendingAccountOptions.EngagementRuleId"/> 0-4.
        /// <c>zb_managed</c> has ZeroBounce provision it and takes
        /// <see cref="ZeroBounceWarmup.AddSendingAccountOptions.EmailContent"/> and
        /// <see cref="ZeroBounceWarmup.AddSendingAccountOptions.ManagedSending"/>; a 200 means
        /// accepted, and the mailbox is provisioned afterwards.
        /// </para>
        /// <para>
        /// The zb_managed body is <c>email_content</c> (<c>company_name</c>,
        /// <c>company_website</c>, <c>company_description</c>, <c>types_of_emails</c>,
        /// <c>example_email</c>) and <c>managed_sending</c> (<c>weekdays_only</c>,
        /// <c>timezone</c>, <c>timezone_offset</c>, <c>warmup_start_date</c>), as documented at
        /// https://www.zerobounce.net/docs/warmup-api/adding-a-sending-account.
        /// </para>
        /// </summary>
        void AddSendingAccount(string fromAddress, string onboardingModel,
            ZeroBounceWarmup.AddSendingAccountOptions options, Action<JToken> ok, Action<string> fail);

        /// <summary>
        /// Changes an account's engagement rule, status and auto-enable flag.
        /// PUT /sending-accounts. <paramref name="statusId"/> is 0 to 8
        /// (4 Paused, 5 Disabled).
        /// </summary>
        void UpdateSendingAccount(string fromAddress, int engagementRuleId, int statusId,
            bool autoEnable, Action<JToken> ok, Action<string> fail);

        /// <summary>Removes a sending account. DELETE /sending-accounts. Destructive and final.</summary>
        void DeleteSendingAccount(string fromAddress, Action<JToken> ok, Action<string> fail);

        /// <summary>
        /// Moves an account and its history to a new from-address.
        /// PUT /smtp-credentials — despite the path, no SMTP credentials are stored.
        /// </summary>
        void ChangeFromAddress(string oldFromAddress, string newFromAddress,
            Action<JToken> ok, Action<string> fail);

        /// <summary>
        /// Registered from-addresses. GET /from-addresses
        /// <para>
        /// <paramref name="searchFrom"/> takes letters, digits and
        /// <c>. - _ @ +</c> only. The paging values go on the wire as strings.
        /// </para>
        /// </summary>
        void ListFromAddresses(string searchFrom, int? offset, int? limit,
            Action<JToken> ok, Action<string> fail);

        /// <summary>-1 Custom, 0 Default, 1 Slow Pace, 2 Repair, 3 Boost, 4 Protect.</summary>
        void GetEngagementRules(Action<JToken> ok, Action<string> fail);

        /// <summary>Sets a standard rule (0 to 4) on an address. PUT /engagement-rule</summary>
        void SetEngagementRule(string cFrom, int ruleId, Action<JToken> ok, Action<string> fail);

        /// <summary>An address's custom rule, with can_update and remaining_seconds.</summary>
        void GetCustomEngagement(string cFrom, Action<JToken> ok, Action<string> fail);

        /// <summary>Creates a custom rule. Starts a 7 day cooldown on updates.</summary>
        void CreateCustomEngagement(string cFrom, int replyPct, int importantPct, int clickPct,
            Action<JToken> ok, Action<string> fail);

        /// <summary>Updates a custom rule. During the 7 day cooldown the API answers 429.</summary>
        void UpdateCustomEngagement(string cFrom, int replyPct, int importantPct, int clickPct,
            Action<JToken> ok, Action<string> fail);

        /// <summary>Deletes a custom rule and falls back to standard rule 1-4.</summary>
        void DeleteCustomEngagement(string cFrom, int engagementRuleId,
            Action<JToken> ok, Action<string> fail);

        /// <summary>The seed list. GET /seeds</summary>
        void GetSeeds(Action<JToken> ok, Action<string> fail);

        /// <summary>The seed list as CSV (columns seed,provider). GET /seeds/download</summary>
        void DownloadSeeds(Action<string> ok, Action<string> fail);

        /// <summary>Number of seeds. GET /seed-count</summary>
        void GetSeedCount(Action<JToken> ok, Action<string> fail);

        /// <summary>Today's warmup progress. GET /progress-today</summary>
        void GetProgressToday(Action<JToken> ok, Action<string> fail);

        /// <summary>Messages sent today. GET /sent-today</summary>
        void GetSentToday(Action<JToken> ok, Action<string> fail);

        /// <summary>Current daily volume. GET /emails-per-day</summary>
        void GetEmailsPerDay(Action<JToken> ok, Action<string> fail);

        /// <summary>Statistics for an address. An address with no history returns zeros.</summary>
        void GetStatistics(string fromAddress, Action<JToken> ok, Action<string> fail);

        /// <summary>Daily statistics between two dates, inclusive. GET /statistics-per-day</summary>
        void GetStatisticsPerDay(string fromAddress, string startDate, string endDate,
            Action<JToken> ok, Action<string> fail);

        /// <summary>Detailed statistics for an address. GET /statistics-detailed</summary>
        void GetStatisticsDetailed(string cFrom, Action<JToken> ok, Action<string> fail);

        /// <summary>An address's statistics as CSV. GET /statistics/download</summary>
        void DownloadStatistics(string cFrom, Action<string> ok, Action<string> fail);

        /// <summary>Statistics for one from-address. GET /from-address-statistics</summary>
        void GetFromAddressStatistics(string fromAddress, Action<JToken> ok, Action<string> fail);

        /// <summary>
        /// SPF, DKIM and DMARC status of a from-address's domain.
        /// GET /check-domain-dmarc
        /// <para>
        /// LIVE: pass an <em>email address</em>. The live API answers
        /// "Please provide a valid email address" for a bare domain, although
        /// the docs describe a domain.
        /// </para>
        /// </summary>
        void CheckDomainDmarc(string fromAddress, string dkimSelector,
            Action<JToken> ok, Action<string> fail);

        /// <summary>
        /// Checks that SMTP credentials connect and hand off a message.
        /// POST /test-smtp-configuration
        /// <para>
        /// Nothing is stored, and the test message goes to a ZeroBounce test
        /// recipient, not <paramref name="fromAddress"/>.
        /// </para>
        /// <para>
        /// LIVE: the API answers any failed SMTP login with HTTP 500. That is
        /// an API bug. The username and password are masked in every error
        /// from this method.
        /// </para>
        /// </summary>
        void TestSmtpConfiguration(string fromAddress, string username, string password,
            string server, int port, Action<JToken> ok, Action<string> fail);
    }
}
