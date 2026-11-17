using Microsoft.AspNetCore.Mvc;
using StudyPlanner.Application.Files;

namespace StudyPlanner.Api.Controllers;

[ApiController]
[Route("api/files")]
public sealed class FilesController(FileService fileService) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<FileDto>>> GetAll(CancellationToken cancellationToken)
    {
        return Ok(await fileService.GetAllAsync(cancellationToken));
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<FileDto>> GetById(Guid id, CancellationToken cancellationToken)
    {
        var result = await fileService.GetByIdAsync(id, cancellationToken);
        return result.IsSuccess ? Ok(result.Value) : ToErrorResponse(result.Error!);
    }

    [HttpGet("{id:guid}/content")]
    public async Task<IActionResult> Download(Guid id, CancellationToken cancellationToken)
    {
        var result = await fileService.DownloadAsync(id, cancellationToken);
        if (!result.IsSuccess)
        {
            return ToErrorResponse(result.Error!);
        }

        var content = result.Value!;
        return File(content.Content, content.ContentType, content.FileName);
    }

    [HttpPost]
    [Consumes("multipart/form-data")]
    public async Task<ActionResult<FileDto>> Upload(IFormFile file, CancellationToken cancellationToken)
    {
        await using var stream = file.OpenReadStream();
        var result = await fileService.UploadAsync(file.FileName, file.ContentType, stream, file.Length, cancellationToken);
        if (!result.IsSuccess)
        {
            return ToErrorResponse(result.Error!);
        }

        return CreatedAtAction(nameof(GetById), new { id = result.Value!.Id }, result.Value);
    }

    [HttpPut("{id:guid}")]
    [Consumes("multipart/form-data")]
    public async Task<ActionResult<FileDto>> Replace(Guid id, IFormFile file, [FromForm] int version, CancellationToken cancellationToken)
    {
        await using var stream = file.OpenReadStream();
        var result = await fileService.ReplaceContentAsync(id, file.FileName, file.ContentType, stream, file.Length, version, cancellationToken);
        return result.IsSuccess ? Ok(result.Value) : ToErrorResponse(result.Error!);
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        var result = await fileService.DeleteAsync(id, cancellationToken);
        return result.IsSuccess ? NoContent() : ToErrorResponse(result.Error!);
    }

    private ObjectResult ToErrorResponse(FileError error)
    {
        var status = error.Type switch
        {
            FileErrorType.NotFound => StatusCodes.Status404NotFound,
            FileErrorType.Conflict => StatusCodes.Status409Conflict,
            _ => StatusCodes.Status400BadRequest,
        };

        var problem = new ProblemDetails { Status = status, Detail = error.Message };
        if (error.Current is not null)
        {
            problem.Extensions["current"] = error.Current;
        }

        return new ObjectResult(problem) { StatusCode = status };
    }
}
