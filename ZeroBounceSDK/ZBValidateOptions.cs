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
    }
}
