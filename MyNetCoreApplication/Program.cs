using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Rewrite;
using Microsoft.VisualBasic;

var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

// Middlewares
app.UseRewriter(new RewriteOptions().AddRedirect("tasks/(.*)", "todos/$1"));
app.Use(async (context, next) =>
{
    Console.WriteLine($"[{context.Request.Method} {context.Request.Path} {DateTime.UtcNow} Started.]");
    await next(context);
    Console.WriteLine($"[{context.Request.Method} {context.Request.Path} {DateTime.UtcNow} Finished.]");
});
app.Use(async (context, next) =>
{
    Console.WriteLine("This is another middleware.");
    await next(context);
    Console.WriteLine("This is another middleware2.");
});


app.MapGet("/", () => "Hello World!");

var toDos = new List<ToDo>();

app.MapGet("/todos", () => toDos);

app.MapGet("/todos/{id}", Results<Ok<ToDo>, NotFound> (int id) =>
{
    Console.WriteLine("Getting a single todo.");
    var targetToDo = toDos.SingleOrDefault(t => id == t.ID);
    return targetToDo is null ? TypedResults.NotFound() : TypedResults.Ok(targetToDo);
});

app.MapPost("/todos", (ToDo task) =>
{
    toDos.Add(task);
    return TypedResults.Created("/todos/{id}", task);
})
.AddEndpointFilter(async (context, next) =>
{
    var taskArgument = context.GetArgument<ToDo>(0);
    var errors = new Dictionary<string, string[]>();
    if (taskArgument.DueDate < DateTime.Now)
    {
        errors.Add(nameof(ToDo.DueDate), ["Cannot have a due date past today."]);
    }
    if (taskArgument.IsCompleted)
    {
        errors.Add(nameof(ToDo.IsCompleted), ["Cannot add a completed todo."]);
    }

    if (errors.Count > 0)
    {
        return Results.ValidationProblem(errors);
    }

    return await next(context);
});

app.MapDelete("/todos/{id}", (int id) =>
{
    toDos.RemoveAll(t => id == t.ID);
    return TypedResults.NoContent();
});

app.Run();

public record ToDo(int ID, string Name, DateTime DueDate, bool IsCompleted);