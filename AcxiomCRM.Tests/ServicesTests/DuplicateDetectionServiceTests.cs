using AcxiomCRM.Services;
using Xunit;

namespace AcxiomCRM.Tests.ServicesTests
{
    public class DuplicateDetectionServiceTests
    {
        private readonly IDuplicateDetectionService _service = new DuplicateDetectionService();

        [Theory]
        [InlineData("9876543210", true)]
        [InlineData("6000000000", true)]
        [InlineData("7123456789", true)]
        [InlineData("8999999999", true)]
        [InlineData("1234567890", false)]  // Starts with 1
        [InlineData("5000000000", false)]  // Starts with 5
        [InlineData("98765", false)]       // Too short
        [InlineData("", false)]
        [InlineData(null, false)]
        public void IsValidPhone_ValidatesIndianMobile(string? phone, bool expected)
        {
            var result = _service.IsValidPhone(phone);
            Assert.Equal(expected, result);
        }

        [Theory]
        [InlineData("+91 98765 43210", "9876543210")]
        [InlineData("09876543210", "9876543210")]   // Leading 0 stripped
        [InlineData("919876543210", "9876543210")]  // 91 country code stripped
        [InlineData("9876543210", "9876543210")]    // Already normalized
        public void NormalizePhone_StripsPrefixes(string input, string expected)
        {
            var result = _service.NormalizePhone(input);
            Assert.Equal(expected, result);
        }

        [Theory]
        [InlineData("User@Example.COM", "user@example.com")]
        [InlineData("  test@test.com  ", "test@test.com")]
        public void NormalizeEmail_LowercasesAndTrims(string input, string expected)
        {
            var result = _service.NormalizeEmail(input);
            Assert.Equal(expected, result);
        }

        [Theory]
        [InlineData("valid@test.com", true)]
        [InlineData("user@domain.co.in", true)]
        [InlineData("notanemail", false)]
        [InlineData("@missing.com", false)]
        [InlineData("", false)]
        [InlineData(null, false)]
        public void IsValidEmail_ValidatesFormat(string? email, bool expected)
        {
            var result = _service.IsValidEmail(email);
            Assert.Equal(expected, result);
        }
    }
}
