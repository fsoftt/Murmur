using Directo.Domain.Connections;
using Directo.Domain.Model;
using Directo.Networking.Signaling;

namespace Directo.Presentation.Formatting;

/// <summary>
/// User-facing wording (architecture §38 and §57): plain language, honest about where a message
/// is, never technical jargon such as "ICE candidate gathering failed".
/// </summary>
public static class UserMessages
{
    public static string ForError(DirectoErrorCode code) => code switch
    {
        DirectoErrorCode.InvalidMessage => "El mensaje está vacío.",
        DirectoErrorCode.MessageTooLarge => "El mensaje es demasiado largo.",
        DirectoErrorCode.ContactBlocked => "Desbloquea este contacto para escribirle.",
        DirectoErrorCode.ContactNotFound => "Este contacto ya no existe.",
        DirectoErrorCode.InviteExpired => "Este código QR ha caducado. Pide uno nuevo.",
        DirectoErrorCode.InviteInvalid => "Este código QR no es una invitación válida de Directo.",
        DirectoErrorCode.PeerUnavailable => "No encontramos a la otra persona. Ambos deben tener la app abierta al emparejar.",
        DirectoErrorCode.PeerConnectionFailed => "No pudimos establecer una conexión directa. Inténtalo de nuevo.",
        DirectoErrorCode.IdentityVerificationFailed => "La otra parte no pudo demostrar su identidad. No se ha guardado nada.",
        DirectoErrorCode.ProtocolVersionUnsupported => "La otra persona usa una versión incompatible. Alguno de los dos debe actualizar la app.",
        DirectoErrorCode.StorageFailure => "No se pudo acceder al almacenamiento local cifrado.",
        _ => "Algo salió mal. Inténtalo de nuevo.",
    };

    public static (string Icon, string Description) ForStatus(MessageStatus status) => status switch
    {
        MessageStatus.Pending => ("🕒", "Pendiente en este dispositivo"),
        MessageStatus.Sent => ("↑", "Transmitido, esperando confirmación"),
        MessageStatus.Delivered => ("✓", "Entregado"),
        MessageStatus.Failed => ("!", "No se pudo entregar"),
        _ => (string.Empty, string.Empty),
    };

    public static string ForConnection(PeerConnectionState state) => state switch
    {
        PeerConnectionState.Connected => "Conectado directamente",
        PeerConnectionState.Negotiating => "Conectando…",
        PeerConnectionState.Reconnecting => "Sin conexión directa. Tus mensajes siguen pendientes en este dispositivo.",
        PeerConnectionState.Discovering => "No está en línea. Los mensajes se entregarán cuando ambos coincidáis.",
        _ => "Sin conexión",
    };

    public static string? ForSignaling(SignalingState state) => state switch
    {
        SignalingState.Connected => null,
        SignalingState.Connecting => "Conectando con el servicio de encuentro…",
        _ => "Sin conexión a Internet. Tus conversaciones siguen guardadas en este dispositivo.",
    };
}
