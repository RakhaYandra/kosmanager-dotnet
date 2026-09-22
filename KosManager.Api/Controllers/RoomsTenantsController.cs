using KosManager.Application.Billing;
using KosManager.Domain;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace KosManager.Api.Controllers;

[ApiController]
[Route("api/rooms")]
[Authorize(Policy = "owner")]
public class RoomsController(RoomService rooms) : ControllerBase
{
    [HttpGet]
    [Authorize]
    public async Task<IActionResult> List() => Ok(await rooms.ListAsync());

    [HttpPost]
    public async Task<IActionResult> Create(Room input)
    {
        var r = await rooms.CreateAsync(input);
        return Created($"/api/rooms/{r.Id}", r);
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, Room input) => Ok(await rooms.UpdateAsync(id, input));

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        await rooms.DeleteAsync(id);
        return NoContent();
    }
}

[ApiController]
[Route("api/tenants")]
[Authorize(Policy = "owner")]
public class TenantsController(TenantService tenants) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List() => Ok(await tenants.ListAsync());

    [HttpPost]
    public async Task<IActionResult> Create(Tenant input)
    {
        var t = await tenants.CreateAsync(input);
        return Created($"/api/tenants/{t.Id}", t);
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, Tenant input) => Ok(await tenants.UpdateAsync(id, input));

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        await tenants.DeleteAsync(id);
        return NoContent();
    }

    [HttpPost("import")]
    public async Task<IActionResult> Import(IFormFile file)
    {
        if (file is null || file.Length > 2 * 1024 * 1024)
            throw new Application.Auth.BadRequestException("csv maks 2MB");
        var (imported, failed) = await tenants.ImportAsync(file.OpenReadStream());
        return Ok(new { imported, failed });
    }
}
