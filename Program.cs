using Votify.Presentation;

Application.EnableVisualStyles();
Application.SetCompatibleTextRenderingDefault(false);

// Aquí irá la inicialización real del DAL y el servicio
// IVotifyService service = new VotifyService(new EntityFrameworkDAL(new VotifyDBContext()));

Application.Run(new FormLogin(/* service */));