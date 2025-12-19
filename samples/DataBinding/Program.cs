using Hex1b;

var customer = new Customer { Id = 1, Name = "Alice" };

using var app = new Hex1bApp(c => c.VStack(c => [
    c.HStack(c => [
        c.Text("Customer ID: "),
        c.Text(customer.Id.ToString())
        ]),
    c.Button("Save").OnClick(async c => {
        await customer.SaveAsync(c.CancellationToken);
        c.Context.RequestStop();
    })
]));

await app.RunAsync();

public class Customer
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public Task SaveAsync(CancellationToken cancellationToken)
    {
        // Simulate async save operation
        return Task.Delay(500, cancellationToken);
    }
}