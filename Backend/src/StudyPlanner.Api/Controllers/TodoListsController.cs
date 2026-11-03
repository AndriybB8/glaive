using Microsoft.AspNetCore.Mvc;
using StudyPlanner.Application.TodoLists;

namespace StudyPlanner.Api.Controllers;

/// <summary>Listy rzeczy do zrobienia. Punkty list: TodoItemsController.</summary>
[ApiController]
[Route("api/todo-lists")]
public sealed class TodoListsController(TodoListService todoListService) : ControllerBase
{
    /// <summary>Listy z liczbą wszystkich i zrobionych punktów.</summary>
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<TodoListSummaryDto>>> GetAll(CancellationToken cancellationToken)
    {
        return Ok(await todoListService.GetAllAsync(cancellationToken));
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<TodoListDto>> GetById(Guid id, CancellationToken cancellationToken)
    {
        var result = await todoListService.GetByIdAsync(id, cancellationToken);
        return result.IsSuccess ? Ok(result.Value) : ToErrorResponse(result.Error!);
    }

    [HttpPost]
    public async Task<ActionResult<TodoListDto>> Create(CreateTodoListRequest request, CancellationToken cancellationToken)
    {
        var result = await todoListService.CreateAsync(request, cancellationToken);
        if (!result.IsSuccess)
        {
            return ToErrorResponse(result.Error!);
        }

        return CreatedAtAction(nameof(GetById), new { id = result.Value!.Id }, result.Value);
    }

    /// <summary>Zmiana nazwy listy.</summary>
    [HttpPut("{id:guid}")]
    public async Task<ActionResult<TodoListDto>> Update(Guid id, UpdateTodoListRequest request, CancellationToken cancellationToken)
    {
        var result = await todoListService.UpdateAsync(id, request, cancellationToken);
        return result.IsSuccess ? Ok(result.Value) : ToErrorResponse(result.Error!);
    }

    /// <summary>Usuwa listę razem z jej punktami.</summary>
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        var result = await todoListService.DeleteAsync(id, cancellationToken);
        return result.IsSuccess ? NoContent() : ToErrorResponse(result.Error!);
    }

    private ObjectResult ToErrorResponse(TodoListError error)
    {
        var status = error.Type switch
        {
            TodoListErrorType.NotFound => StatusCodes.Status404NotFound,
            TodoListErrorType.Conflict => StatusCodes.Status409Conflict,
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
