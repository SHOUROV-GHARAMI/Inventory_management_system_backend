using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using InventoryManagement.API.Services;

namespace InventoryManagement.API.Controllers;

[ApiController]
[Route("api/custom-id")]
[Authorize]
public class CustomIdController : ControllerBase
{
    private readonly ICustomIdService _customIdService;

    public CustomIdController(ICustomIdService customIdService)
    {
        _customIdService = customIdService;
    }

    [HttpPost("preview")]
    public ActionResult<CustomIdPreviewDto> PreviewFormat([FromBody] CustomIdFormatDto dto)
    {
        if (!_customIdService.ValidateFormat(dto.Format))
        {
            return BadRequest(new { message = "Invalid format string" });
        }

        var preview = _customIdService.PreviewId(dto.Format);

        return Ok(new CustomIdPreviewDto
        {
            Format = dto.Format,
            Preview = preview
        });
    }

    [HttpPost("validate")]
    public ActionResult<bool> ValidateFormat([FromBody] CustomIdFormatDto dto)
    {
        var isValid = _customIdService.ValidateFormat(dto.Format);
        return Ok(new { isValid, format = dto.Format });
    }
}
