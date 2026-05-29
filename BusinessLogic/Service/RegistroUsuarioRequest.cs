namespace Votify.BusinessLogic.Service
{
    public sealed class RegistroUsuarioRequest
    {
        public string Username { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
        
        public RegistroUsuarioRequest() { }

        public RegistroUsuarioRequest(string username, string email, string password)
        {
            Username = username;
            Email = email;
            Password = password;
        }
    }
}
