using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.WebUtilities;
using System.Text;
using TheCertMaster.Models;

namespace TheCertMaster.Services
{
    public class PasswordResetLinkService
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IEmailService _emailService;
        private readonly IConfiguration _config;

        public PasswordResetLinkService(UserManager<ApplicationUser> userManager, IEmailService emailService, IConfiguration config)
        {
            _userManager = userManager;
            _emailService = emailService;
            _config = config;
        }

        public async Task SendResetLinkAsync(ApplicationUser user, string requestScheme, string requestHost)
        {
            var token = await _userManager.GeneratePasswordResetTokenAsync(user);
            var encodedToken = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(token));

            var configuredBaseUrl = _config["PublicApp:BaseUrl"]?.Trim().TrimEnd('/');
            var baseUrl = !string.IsNullOrWhiteSpace(configuredBaseUrl)
                ? configuredBaseUrl
                : $"{requestScheme}://{requestHost}";

            var resetUrl =
                $"{baseUrl}/reset-password.html?email={Uri.EscapeDataString(user.Email ?? string.Empty)}&code={Uri.EscapeDataString(encodedToken)}";

            var body =
                $"A password reset has been requested for your The Cert Master account.{Environment.NewLine}{Environment.NewLine}" +
                $"Open this link to set a new password:{Environment.NewLine}{resetUrl}{Environment.NewLine}{Environment.NewLine}" +
                $"If you did not expect this email, you can ignore it.";

            await _emailService.SendAsync(user.Email ?? string.Empty, "Reset your The Cert Master password", body);
        }
    }
}
