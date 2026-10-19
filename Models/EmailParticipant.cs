using System.Text.Json.Serialization;

namespace Safi_Ticket.Models;

/// <summary>One person on an email: who it was from, to, or copied to.</summary>
public sealed record EmailParticipant(string? Name, string Email)
{
    [JsonIgnore]
    public string DisplayName => string.IsNullOrWhiteSpace(Name) ? Email : Name;
}
