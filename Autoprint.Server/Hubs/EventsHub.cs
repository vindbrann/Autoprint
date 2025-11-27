using Microsoft.AspNetCore.SignalR;

namespace Autoprint.Server.Hubs
{
    // Cette classe agit comme la "Tour de Contrôle"
    public class EventsHub : Hub
    {
        // On pourrait mettre des méthodes ici si le client devait envoyer des messages,
        // mais dans notre architecture, le client ne fait qu'écouter.
        // La classe reste donc vide pour le moment.
    }
}