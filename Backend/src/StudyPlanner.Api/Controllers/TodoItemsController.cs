using Microsoft.AspNetCore.Mvc;
using StudyPlanner.Application.TodoLists;

namespace StudyPlanner.Api.Controllers;

/// <summary>Lista i dodawanie pod listą rzeczy do zrobienia, pozostałe operacje bezpośrednio na punkcie.</summary>
[ApiController]
public sealed class TodoItemsController(TodoItemService todoItemService) : ControllerBase
{
    [HttpGet("api/todo-lists/{listId:guid}/items")]
    public async Task<ActionResult<IReadOnlyList<TodoItemDto>>> GetByList(Guid listId, CancellationToken cancellationToken)
    {
        var result = await todoItemService.GetByListAsync(listId, cancellationToken);
        return result.IsSuccess ? Ok(result.Value) : ToErrorResponse(result.Error!);
    }

    [HttpPost("api/todo-lists/{listId:guid}/items")]
    public async Task<ActionResult<TodoItemDto>> Create(Guid listId, CreateTodoItemRequest request, CancellationToken cancellationToken)
    {
        var result = await todoItemService.CreateAsync(listId, request, cancellationToken);
        if (!result.IsSuccess)
        {
            return ToErrorResponse(result.Error!);
        }

        return CreatedAtAction(nameof(GetById), new { id = result.Value!.Id }, result.Value);
    }

    [HttpGet("api/todo-items/{id:guid}")]
    public async Task<ActionResult<TodoItemDto>> GetById(Guid id, CancellationToken cancellationToken)
    {
        var result = await todoItemService.GetByIdAsync(id, cancellationToken);
        return result.IsSuccess ? Ok(result.Value) : ToErrorResponse(result.Error!);
    }

    [HttpPut("api/todo-items/{id:guid}")]
    public async Task<ActionResult<TodoItemDto>> Update(Guid id, UpdateTodoItemRequest request, CancellationToken cancellationToken)
    {
        var result = await todoItemService.UpdateAsync(id, request, cancellationToken);
        return result.IsSuccess ? Ok(result.Value) : ToErrorResponse(result.Error!);
    }

    /// <summary>Zaznaczenie / odznaczenie punktu – checkbox na liście.</summary>
    [HttpPatch("api/todo-items/{id:guid}/done")]
    public async Task<ActionResult<TodoItemDto>> SetDone(Guid id, SetTodoItemDoneRequest request, CancellationToken cancellationToken)
    {
        var result = await todoItemService.SetDoneAsync(id, request, cancellationToken);
        return result.IsSuccess ? Ok(result.Value) : ToErrorResponse(result.Error!);
    }

    /// <summary>
    /// Nowa kolejność punktów listy (przeciąganie). Body: <c>{ "itemIds": [...] }</c> – wszystkie punkty listy, od góry.
    /// 409, gdy lista zmieniła się w międzyczasie (punkt dodany lub usunięty w innej karcie).
    /// </summary>
    [HttpPut("api/todo-lists/{listId:guid}/items/order")]
    public async Task<ActionResult<IReadOnlyList<TodoItemDto>>> Reorder(Guid listId, ReorderTodoItemsRequest request, CancellationToken cancellationToken)
    {
        var result = await todoItemService.ReorderAsync(listId, request, cancellationToken);
        return result.IsSuccess ? Ok(result.Value) : ToErrorResponse(result.Error!);
    }

    [HttpDelete("api/todo-items/{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        var result = await todoItemService.DeleteAsync(id, cancellationToken);
        return result.IsSuccess ? NoContent() : ToErrorResponse(result.Error!);
    }

    private ObjectResult ToErrorResponse(TodoItemError error)
    {
        var status = error.Type switch
        {
            TodoItemErrorType.NotFound => StatusCodes.Status404NotFound,
            TodoItemErrorType.Conflict => StatusCodes.Status409Conflict,
            _ => StatusCodes.Status400BadRequest,
        };

        var problem = new ProblemDetails { Status = status, Detail = error.Message };
        if (error.Current is not null)
        {
            // Frontend używa tego, żeby pokazać aktualną wersję przy konflikcie.
            problem.Extensions["current"] = error.Current;
        }

        return new ObjectResult(problem) { StatusCode = status };
    }
}
