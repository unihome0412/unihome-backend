using System.Security.Cryptography;
using System.Text;
using UniHome.Application.OccupancyLinks.Abstractions;
using UniHome.Domain.OccupancyLinks;

namespace UniHome.Infrastructure.OccupancyLinks;

/// <summary>
/// Código de 8 caracteres en mayúsculas, sin 0/O/1/I para evitar confusión al
/// copiarlo o leerlo en voz alta.
/// </summary>
public sealed class RandomUnitInvitationCodeGenerator : IUnitInvitationCodeGenerator
{
    private const string Alphabet = "23456789ABCDEFGHJKMNPQRSTUVWXYZ";

    public string Generate()
    {
        var builder = new StringBuilder(UnitInvitation.CodeLength);

        for (var index = 0; index < UnitInvitation.CodeLength; index++)
        {
            var position = RandomNumberGenerator.GetInt32(0, Alphabet.Length);
            builder.Append(Alphabet[position]);
        }

        return builder.ToString();
    }
}
