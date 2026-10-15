using System.Text.RegularExpressions;

namespace AcxiomCRM.Services
{
    public interface IDuplicateDetectionService
    {
        string NormalizeEmail(string? email);
        string NormalizePhone(string? phone);
        bool IsValidPhone(string? phone);
        bool IsValidEmail(string? email);
    }

    public class DuplicateDetectionService : IDuplicateDetectionService
    {
        private static readonly Regex PhoneDigitsRegex = new(@"\D", RegexOptions.Compiled);
        private static readonly Regex StrictIndiaPhoneRegex = new(@"^[6-9]\d{9}$", RegexOptions.Compiled);
        private static readonly Regex EmailRegex = new(@"^[^@\s]+@[^@\s]+\.[^@\s]+$", RegexOptions.Compiled | RegexOptions.IgnoreCase);

        public string NormalizeEmail(string? email)
        {
            if (string.IsNullOrWhiteSpace(email)) return string.Empty;
            return email.Trim().ToLowerInvariant();
        }

        public string NormalizePhone(string? phone)
        {
            if (string.IsNullOrWhiteSpace(phone)) return string.Empty;
            var digitsOnly = PhoneDigitsRegex.Replace(phone.Trim(), "");

            // If it starts with 91 and has 12 digits, strip country code for India standard 10 digits
            if (digitsOnly.Length == 12 && digitsOnly.StartsWith("91"))
            {
                digitsOnly = digitsOnly.Substring(2);
            }
            // If it starts with 0 and has 11 digits, strip leading 0
            else if (digitsOnly.Length == 11 && digitsOnly.StartsWith("0"))
            {
                digitsOnly = digitsOnly.Substring(1);
            }

            return digitsOnly;
        }

        public bool IsValidPhone(string? phone)
        {
            if (string.IsNullOrWhiteSpace(phone)) return false;
            var normalized = NormalizePhone(phone);
            // Strict 10-digit mobile rule starting with 6,7,8,9
            return StrictIndiaPhoneRegex.IsMatch(normalized);
        }

        public bool IsValidEmail(string? email)
        {
            if (string.IsNullOrWhiteSpace(email)) return false;
            return EmailRegex.IsMatch(email.Trim());
        }
    }
}
