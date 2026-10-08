namespace Directo.Security;

/// <summary>Authentication or key agreement failure. Never carries secret material in its message.</summary>
public sealed class CryptoException(string message) : Exception(message);
