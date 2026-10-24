namespace ZeroBounceSDK
{
    /// <summary>Optional query/body parameters for validate and validatebatch.</summary>
    public class ZBValidateOptions
    {
        /// <summary>Validation timeout in seconds (3–60 for single validate).</summary>
        public int? Timeout { get; set; }

        /// <summary>Maps to activity_data.</summary>
        public bool? ActivityData { get; set; }

        /// <summary>Maps to verify_plus.</summary>
        public bool? VerifyPlus { get; set; }

        /// <summary>Maps to domain_info. Fills DomainWebsiteExists and DomainRegistrantCompanyName.</summary>
        public bool? DomainInfo { get; set; }

        /// <summary>Maps to greylist_processing. Retries a greylisted address in the background.</summary>
        public bool? GreylistProcessing { get; set; }

        /// <summary>
        /// Maps to callback_url: where the greylist retry posts its result. It
        /// needs <see cref="GreylistProcessing"/> set to true and is rejected
        /// without it, and when blank, before any request is sent.
        /// </summary>
        public string CallbackUrl { get; set; }

        /// <summary>When true, sends form-encoded POST instead of GET.</summary>
        public bool UsePost { get; set; }
    }
}
