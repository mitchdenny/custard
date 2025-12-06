using Custard;
using Custard.Widgets;

// Set up cancellation with Ctrl+C
using var cts = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) =>
{
    e.Cancel = true; // Prevent immediate termination
    cts.Cancel();
};

// Sample data - mutable contact model
var contacts = new List<Contact>
{
    new("1", "Alice Johnson", "alice.johnson@example.com"),
    new("2", "Bob Smith", "bob.smith@example.com"),
    new("3", "Carol Williams", "carol.williams@example.com"),
    new("4", "David Brown", "david.brown@example.com"),
    new("5", "Eve Davis", "eve.davis@example.com"),
};

// Convert to list items for display
ListItem[] ToListItems() => contacts.Select(c => new ListItem(c.Id, c.Name)).ToArray();

// State for the list
var listState = new ListState { Items = ToListItems() };

// State for the detail form
var nameState = new TextBoxState();
var emailState = new TextBoxState();

// Helper to get current contact
Contact? GetSelectedContact() => 
    listState.SelectedItem is { } item ? contacts.Find(c => c.Id == item.Id) : null;

// When selection changes, update the detail form
listState.OnSelectionChanged = item =>
{
    var contact = contacts.Find(c => c.Id == item.Id);
    if (contact != null)
    {
        nameState.Text = contact.Name;
        nameState.CursorPosition = nameState.Text.Length;
        nameState.ClearSelection();
        emailState.Text = contact.Email;
        emailState.CursorPosition = emailState.Text.Length;
        emailState.ClearSelection();
    }
};

// Save action - updates the contact in the list
void Save()
{
    var contact = GetSelectedContact();
    if (contact != null)
    {
        contact.Name = nameState.Text;
        contact.Email = emailState.Text;
        // Refresh the list display
        listState.Items = ToListItems();
    }
}

// Initialize with first contact
if (listState.SelectedItem != null)
{
    listState.OnSelectionChanged(listState.SelectedItem);
}

// Create and run the app
using var app = new CustardApp(ct => App(listState, nameState, emailState, Save, cts, ct));
await app.RunAsync(cts.Token);

// The root component - master-detail layout
static Task<CustardWidget> App(
    ListState listState, 
    TextBoxState nameState, 
    TextBoxState emailState,
    Action onSave,
    CancellationTokenSource cts, 
    CancellationToken cancellationToken)
{
    var masterPane = new VStackWidget([
        new ListWidget(listState)
    ]);

    var detailPane = new VStackWidget([
        new HStackWidget([new TextBlockWidget("Name:  "), new TextBoxWidget(nameState)]),
        new HStackWidget([new TextBlockWidget("Email: "), new TextBoxWidget(emailState)]),
        new TextBlockWidget(""),
        new ButtonWidget("Save", onSave),
        new ButtonWidget("Close", () => cts.Cancel())
    ]);

    return Task.FromResult<CustardWidget>(new SplitterWidget(masterPane, detailPane, 25));
}

// Mutable contact model
class Contact(string id, string name, string email)
{
    public string Id { get; } = id;
    public string Name { get; set; } = name;
    public string Email { get; set; } = email;
}