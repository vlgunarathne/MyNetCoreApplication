using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Rewrite;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddSingleton<ITaskService>(new InMemoryTaskService());
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

app.MapGet("/todos", (ITaskService service) => service.GetToDos());

app.MapGet("/todos/{id}", Results<Ok<ToDo>, NotFound> (int id, ITaskService service) =>
{
    Console.WriteLine("Getting a single todo.");
    var targetToDo = service.GetToDoById(id);
    return targetToDo is null ? TypedResults.NotFound() : TypedResults.Ok(targetToDo);
});

app.MapPost("/todos", (ToDo task, ITaskService service) =>
{
    return TypedResults.Created("/todos/{id}", service.AddToDo(task));
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

app.MapDelete("/todos/{id}", (int id, ITaskService service) =>
{
    service.DeleteTodoById(id);
    return TypedResults.NoContent();
});

app.Run();

public record ToDo(int ID, string Name, DateTime DueDate, bool IsCompleted);

interface ITaskService
{
    ToDo? GetToDoById(int id);

    List<ToDo> GetToDos();

    void DeleteTodoById(int id);

    ToDo AddToDo(ToDo newTodo);
}

class InMemoryTaskService : ITaskService
{
    private readonly List<ToDo> _todos = [];
    public ToDo AddToDo(ToDo newTodo)
    {
        _todos.Add(newTodo);
        return newTodo;
    }

    public void DeleteTodoById(int id)
    {
        _todos.RemoveAll(t => t.ID == id);
    }

    public ToDo? GetToDoById(int id) => _todos.SingleOrDefault(t => t.ID == id);

    public List<ToDo> GetToDos()
    {
        return _todos;
    }
}