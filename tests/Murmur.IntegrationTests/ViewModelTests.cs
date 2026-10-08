using Murmur.Domain.Model;
using Murmur.IntegrationTests.Support;
using Murmur.Networking.Links;
using Murmur.Presentation.Services;
using Murmur.Presentation.ViewModels;

namespace Murmur.IntegrationTests;

public sealed class ViewModelTests(SignalingServerFixture server) : IClassFixture<SignalingServerFixture>, IAsyncLifetime
{
    private readonly InMemoryPeerLinkNetwork _network = new();
    private readonly FakeNavigator _navigator = new();
    private TestDevice _ana = null!;
    private TestDevice _beto = null!;

    public async Task InitializeAsync()
    {
        _ana = await TestDevice.StartAsync("Ana", server, _network);
        _beto = await TestDevice.StartAsync("Beto", server, _network);
    }

    public async Task DisposeAsync()
    {
        await _ana.DisposeAsync();
        await _beto.DisposeAsync();
    }

    [Fact]
    public async Task Scanning_an_invite_pairs_and_opens_the_paired_screen()
    {
        var invite = new InviteViewModel(_beto.Client, new InlineDispatcher(), new FakeNavigator());
        await invite.CreateAsync();
        var scanner = new ScanInviteViewModel(_ana.Client, _navigator);

        await scanner.AcceptAsync(invite.InviteText!);

        Assert.Null(scanner.ErrorMessage);
        Assert.Equal((await _ana.SingleContactAsync()).Id, Assert.Single(_navigator.OpenedPaired));

        var paired = new PairedViewModel(_ana.Client, _navigator);
        await paired.LoadAsync((await _ana.SingleContactAsync()).Id);
        Assert.Equal("Beto", paired.DisplayName);
        Assert.Equal("B", paired.Initials);
        Assert.Equal("A", paired.MyInitials);
    }

    [Fact]
    public async Task Invalid_invite_shows_a_plain_language_error()
    {
        var scanner = new ScanInviteViewModel(_ana.Client, _navigator);

        await scanner.AcceptAsync("https://example.com/not-an-invite");

        Assert.Equal("Este código QR no es una invitación válida de Murmur.", scanner.ErrorMessage);
        Assert.Empty(_navigator.OpenedPaired);
    }

    [Fact]
    public async Task Chat_shows_a_message_as_pending_and_then_delivered()
    {
        await PairAsync();
        await _beto.StopAsync();
        using var chat = new ChatViewModel(_ana.Client, new InlineDispatcher(), _navigator);
        await chat.LoadAsync((await _ana.SingleContactAsync()).Id);
        Assert.False(chat.SendCommand.CanExecute(null));

        chat.Draft = "Hola";
        Assert.True(chat.SendCommand.CanExecute(null));
        await chat.SendCommand.ExecuteAsync(null);

        var item = Assert.Single(chat.Messages);
        Assert.Equal(string.Empty, chat.Draft);
        Assert.Equal(MessageStatus.Pending, item.Status);
        Assert.Equal("Pendiente en este dispositivo", item.StatusDescription);

        await _beto.StartAsync();
        await Eventually.TrueAsync(() => item.Status == MessageStatus.Delivered, "the chat shows the delivery");
        Assert.Equal("✓", item.StatusIcon);
    }

    [Fact]
    public async Task Consecutive_messages_from_one_side_form_a_bubble_group()
    {
        await PairAsync();
        await _beto.StopAsync();
        using var chat = new ChatViewModel(_ana.Client, new InlineDispatcher(), _navigator);
        await chat.LoadAsync((await _ana.SingleContactAsync()).Id);

        foreach (var text in new[] { "uno", "dos", "tres" })
        {
            chat.Draft = text;
            await chat.SendCommand.ExecuteAsync(null);
        }

        Assert.Equal([true, false, false], chat.Messages.Select(m => m.IsFirstInGroup));
        Assert.Equal([false, false, true], chat.Messages.Select(m => m.IsLastInGroup));
        Assert.Equal(new BubbleCorners(20, 6, 20, 6), chat.Messages[1].Corners);
        Assert.Equal("B", chat.Initials);
    }

    [Fact]
    public async Task Onboarding_is_remembered_and_saves_the_profile_name()
    {
        var onboarding = new OnboardingViewModel(_ana.Client) { ProfileName = "Ana López" };
        var completed = false;
        onboarding.Completed += (_, _) => completed = true;
        Assert.False(await _ana.Client.IsOnboardedAsync());

        await onboarding.StartCommand.ExecuteAsync(null);

        Assert.True(completed);
        Assert.True(await _ana.Client.IsOnboardedAsync());
        Assert.Equal("Ana López", await _ana.Client.GetProfileNameAsync());
    }

    [Fact]
    public async Task Incoming_messages_appear_in_the_open_chat()
    {
        await PairAsync();
        using var chat = new ChatViewModel(_ana.Client, new InlineDispatcher(), _navigator);
        await chat.LoadAsync((await _ana.SingleContactAsync()).Id);

        await _beto.Client.SendMessageAsync((await _beto.SingleContactAsync()).Id, "¿Qué tal?");

        await Eventually.TrueAsync(() => chat.Messages.Any(m => m.Body == "¿Qué tal?" && !m.IsOutgoing), "the incoming message is shown");
    }

    [Fact]
    public async Task Conversation_list_shows_pending_messages()
    {
        await PairAsync();
        await _beto.StopAsync();
        await _ana.Client.SendMessageAsync((await _ana.SingleContactAsync()).Id, "pendiente");
        using var contacts = new ContactsViewModel(_ana.Client, new InlineDispatcher(), _navigator);

        await contacts.LoadAsync();

        var item = Assert.Single(contacts.Conversations);
        Assert.Equal("Beto", item.DisplayName);
        Assert.Equal("pendiente", item.Preview);
        Assert.True(item.HasPending);
    }

    [Fact]
    public async Task Both_sides_see_the_same_safety_number_and_can_mark_it_verified()
    {
        await PairAsync();
        var anaView = new ContactDetailsViewModel(_ana.Client, _navigator);
        var betoView = new ContactDetailsViewModel(_beto.Client, _navigator);
        await anaView.LoadAsync((await _ana.SingleContactAsync()).Id);
        await betoView.LoadAsync((await _beto.SingleContactAsync()).Id);

        Assert.Equal(anaView.SafetyNumber, betoView.SafetyNumber);
        Assert.Equal(anaView.FingerprintCells, betoView.FingerprintCells);
        Assert.Equal(12, anaView.SafetyGroups.Count);
        await anaView.SetVerifiedCommand.ExecuteAsync(true);

        Assert.Equal(VerificationState.Verified, (await _ana.SingleContactAsync()).Verification);
    }

    private async Task PairAsync()
    {
        var invite = await _beto.Client.CreateInviteAsync();
        await _ana.Client.AcceptInviteAsync(invite.Text);
        await Eventually.TrueAsync(async () => (await _beto.Client.Contacts.ListAsync()).Count == 1, "Beto stored the contact");
    }

    private sealed class FakeNavigator : INavigator
    {
        public List<ContactId> OpenedChats { get; } = [];

        public Task OpenChatAsync(ContactId contactId)
        {
            OpenedChats.Add(contactId);
            return Task.CompletedTask;
        }

        public List<ContactId> OpenedPaired { get; } = [];

        public Task OpenContactDetailsAsync(ContactId contactId) => Task.CompletedTask;

        public Task OpenPairedAsync(ContactId contactId)
        {
            OpenedPaired.Add(contactId);
            return Task.CompletedTask;
        }

        public Task OpenInviteAsync() => Task.CompletedTask;

        public Task OpenScannerAsync() => Task.CompletedTask;

        public Task GoBackAsync() => Task.CompletedTask;
    }
}
