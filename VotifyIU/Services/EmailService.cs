using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;

namespace VotifyIU.Services
{
    public interface IEmailService
    {
        Task SendPasswordResetAsync(string toEmail, string resetLink);
    }

    public class EmailService : IEmailService
    {
        private readonly IConfiguration _config;

        public EmailService(IConfiguration config)
        {
            _config = config;
        }

        public async Task SendPasswordResetAsync(string toEmail, string resetLink)
        {
            var smtp = _config.GetSection("Smtp");

            var message = new MimeMessage();
            message.From.Add(new MailboxAddress(smtp["FromName"], smtp["Username"]));
            message.To.Add(MailboxAddress.Parse(toEmail));
            message.Subject = "Recuperación de contraseña - Votify";

            message.Body = new TextPart("html")
            {
                Text = $"""
                    <div style="font-family:sans-serif;max-width:480px;margin:auto;padding:32px;background:#f5f9f2;border-radius:12px;">
                        <h2 style="color:#1A281B;">Recuperar contraseña</h2>
                        <p style="color:#4C6B4F;">Has solicitado restablecer tu contraseña en <strong>Votify</strong>.</p>
                        <p style="color:#4C6B4F;">Haz clic en el botón para crear una nueva contraseña. El enlace expira en <strong>10 mins</strong>.</p>
                        <a href="{resetLink}"
                           style="display:inline-block;margin-top:16px;padding:12px 28px;background:#6C8A6C;color:#fff;border-radius:8px;text-decoration:none;font-weight:bold;">
                            Restablecer contraseña
                        </a>
                        <p style="margin-top:24px;color:#A6AAA9;font-size:12px;">Si no solicitaste esto, ignora este correo.</p>
                    </div>
                """
            };

            using var client = new SmtpClient();
            // Bypass certificate validation (desarrollo — red con proxy/intercepción TLS)
            client.ServerCertificateValidationCallback = (s, c, h, e) => true;
            await client.ConnectAsync(smtp["Host"], int.Parse(smtp["Port"]!), SecureSocketOptions.Auto);
            await client.AuthenticateAsync(smtp["Username"], smtp["Password"]);
            await client.SendAsync(message);
            await client.DisconnectAsync(true);
        }
    }
}
