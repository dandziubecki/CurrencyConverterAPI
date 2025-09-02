namespace CurrencyConverterAPI;

public static class ApiConstants
{
    public static class Authentication
    {
        public const string AdminRole = "Admin";
        public const string UserRole = "User";
        public const string AdminOnlyPolicy = "AdminOnly";
        public const string UserPolicy = "User";
    }

    public static class Configuration
    {
        public const string JwtKey = "Jwt:Key";
        public const string JwtIssuer = "Jwt:Issuer";
        public const string JwtAudience = "Jwt:Audience";
        public const string FrankfurterApiBaseUrl = "FrankfurterApi:BaseUrl";
        public const string RateLimitingPermitLimit = "RateLimiting:PermitLimit";
        public const string RateLimitingWindow = "RateLimiting:Window";
        public const string RateLimitingQueueLimit = "RateLimiting:QueueLimit";
    }

    public static class HttpClients
    {
        public const string Frankfurter = "Frankfurter";
    }

    public static class RateLimiting
    {
        public const string FixedPolicy = "fixed";
    }

    public static class Headers
    {
        public const string CurrencyProvider = "X-Currency-Provider";
    }
}
