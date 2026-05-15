using System.Threading.Tasks;

namespace GadgetVault.Services
{
    public interface IEmailService
    {
        Task<bool> SendEmailAsync(string toEmail, string subject, string body);
        Task<bool> SendInviteEmailAsync(string toEmail, string username, string tempPassword);
    }
}
