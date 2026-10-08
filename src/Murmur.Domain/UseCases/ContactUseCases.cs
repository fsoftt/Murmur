using Murmur.Domain.Common;
using Murmur.Domain.Model;
using Murmur.Domain.Ports;

namespace Murmur.Domain.UseCases;

public sealed class ManageContacts(IContactRepository contacts, IConversationRepository conversations, ChatEvents events)
{
    public Task<IReadOnlyList<Contact>> ListAsync(CancellationToken cancellationToken = default) => contacts.ListAsync(cancellationToken);

    public Task<Contact?> GetAsync(ContactId id, CancellationToken cancellationToken = default) => contacts.GetAsync(id, cancellationToken);

    public Task RenameAsync(ContactId id, string displayName, CancellationToken cancellationToken = default) =>
        UpdateAsync(id, c => c with { DisplayName = Contact.NormalizeDisplayName(displayName, c.DisplayName) }, cancellationToken);

    /// <summary>Blocked contacts are not looked for on the network and their sessions are refused.</summary>
    public Task SetBlockedAsync(ContactId id, bool blocked, CancellationToken cancellationToken = default) =>
        UpdateAsync(id, c => c with { IsBlocked = blocked }, cancellationToken);

    /// <summary>Records that the user compared safety numbers in person or over a trusted channel.</summary>
    public Task MarkVerifiedAsync(ContactId id, bool verified, CancellationToken cancellationToken = default) =>
        UpdateAsync(id, c => c with { Verification = verified ? VerificationState.Verified : VerificationState.Unverified }, cancellationToken);

    public async Task DeleteAsync(ContactId id, CancellationToken cancellationToken = default)
    {
        await contacts.DeleteAsync(id, cancellationToken).ConfigureAwait(false);
        events.OnContactsChanged();
    }

    /// <summary>"Delete for me": removes local history only. It cannot erase the peer's copy.</summary>
    public Task ClearConversationAsync(ConversationId id, CancellationToken cancellationToken = default) =>
        conversations.ClearAsync(id, cancellationToken);

    private async Task UpdateAsync(ContactId id, Func<Contact, Contact> change, CancellationToken cancellationToken)
    {
        var contact = await contacts.GetAsync(id, cancellationToken).ConfigureAwait(false)
            ?? throw new MurmurException(MurmurErrorCode.ContactNotFound, "Contact not found.");
        await contacts.UpdateAsync(change(contact), cancellationToken).ConfigureAwait(false);
        events.OnContactsChanged();
    }
}
